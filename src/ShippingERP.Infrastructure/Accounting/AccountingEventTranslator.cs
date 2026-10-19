using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Accounting;
using ShippingERP.Domain.Entities.Integration;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Integration;
using ShippingERP.Infrastructure.Persistence;
using R = ShippingERP.Application.IntegrationEvents.AccountRoles;

namespace ShippingERP.Infrastructure.Accounting;

/// <summary>What one operational event means for the general ledger.</summary>
public sealed class PostingPlan
{
    public string EventType { get; init; } = string.Empty;

    public string SourceType { get; init; } = string.Empty;

    public long SourceId { get; init; }

    public string SourceNumber { get; init; } = string.Empty;

    /// <summary>Business idempotency key: one journal entry per key (unique index).</summary>
    public string SourceKey { get; init; } = string.Empty;

    public JournalType JournalType { get; init; }

    public DateTime PostingDateUtc { get; init; }

    public long CurrencyId { get; init; }

    public string Description { get; init; } = string.Empty;

    /// <summary>The event cancels an earlier document (cancelled voucher, reversed settlement, cancelled invoice).</summary>
    public bool IsReversal { get; init; }

    /// <summary>For a cancellation: the SourceKey of the entry to reverse exactly (when it is found).</summary>
    public string? ReverseSourceKey { get; init; }

    /// <summary>
    /// For a step-6 cancellation (voucher cancelled, settlement reversed): reverse exactly the latest posted automatic
    /// entry of the same document (SourceType + SourceId) with the same total, when there is one.
    /// </summary>
    public bool ReverseLatestOfSource { get; init; }

    /// <summary>Branch of the lines without one (receivables events carry the document branch).</summary>
    public long? BranchId { get; init; }

    public IReadOnlyList<PostingLine> Lines { get; init; } = Array.Empty<PostingLine>();

    /// <summary>Set when the event has no accounting effect: nothing is posted (status Skipped).</summary>
    public string? NoImpactReason { get; init; }
}

/// <summary>
/// Turns an outbox message into a <see cref="PostingPlan"/> (step 7):
///
///   InvoiceIssued           Dr Accounts receivable (total)
///                           Cr Shipping revenue / COD commission revenue / additional service revenue (taxable amount,
///                              split with the pricing snapshots of the invoiced shipments)
///                           Cr Tax payable (tax)
///   InvoiceCancelled        exact reversal of the InvoiceIssued entry
///   CreditNoteIssued        Dr the same revenue split as the invoice entry  Dr Tax payable  Cr Accounts receivable
///   CustomerPaymentReceived Dr Cash clearing (cash) / Bank (bank transfer, card, wallet)  Cr Accounts receivable
///   CustomerPaymentAllocated no effect (the receivable is per customer in the ledger; allocation is sub-ledger only)
///   Step 6 events           their own AccountingPostingRequest (Posting)
///   anything else           no effect
/// </summary>
public sealed class AccountingEventTranslator
{
    private static readonly Dictionary<string, JournalType> Step6Journals = new(StringComparer.Ordinal)
    {
        ["CodCollected"] = JournalType.Cod,
        ["CodCollectionAdjusted"] = JournalType.Cod,
        ["DriverCashHandedOver"] = JournalType.Cod,
        ["DriverCashAdjusted"] = JournalType.Cod,
        ["SenderSettlementPaid"] = JournalType.SenderSettlement,
        ["SenderSettlementReversed"] = JournalType.SenderSettlement,
        ["CashReceiptPosted"] = JournalType.Cash,
        ["CashPaymentPosted"] = JournalType.Cash,
        ["CashVoucherCancelled"] = JournalType.Cash,
        ["CashTransferApproved"] = JournalType.Cash,
        ["CashTransferReceived"] = JournalType.Cash,
        ["CashboxOpeningBalance"] = JournalType.Cash,
        ["CashboxDiscrepancyPosted"] = JournalType.Cash
    };

    private readonly AppDbContext _dbContext;

    public AccountingEventTranslator(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public static string KeyOf(string eventType, string sourceType, long sourceId) =>
        string.Create(CultureInfo.InvariantCulture, $"{eventType}:{sourceType}:{sourceId}");

    public async Task<PostingPlan> TranslateAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        switch (message.EventType)
        {
            case "InvoiceIssued":
                return await InvoiceIssuedAsync(Read<InvoiceIssuedEvent>(message), cancellationToken);
            case "InvoiceCancelled":
                return await InvoiceCancelledAsync(Read<InvoiceCancelledEvent>(message), cancellationToken);
            case "CreditNoteIssued":
                return await CreditNoteAsync(Read<CreditNoteIssuedEvent>(message), cancellationToken);
            case "CustomerPaymentReceived":
                return CustomerPayment(Read<CustomerPaymentReceivedEvent>(message));
            case "CustomerPaymentAllocated":
                return NoImpact(message, "تخصيص دفعة مسجلة سابقاً على الفواتير لا يغيّر رصيد ذمم العميل في الأستاذ العام.");
        }

        using var document = JsonDocument.Parse(message.PayloadJson);
        if (!document.RootElement.TryGetProperty("posting", out var postingElement) || postingElement.ValueKind != JsonValueKind.Object)
        {
            return NoImpact(message, "الحدث ليس حدثاً مالياً.");
        }

        var posting = postingElement.Deserialize<AccountingPostingRequest>(OutboxIntegrationEventPublisher.JsonOptions)
                      ?? throw new BusinessRuleException("قيد الحدث غير مقروء.");

        if (posting.Lines.Count == 0)
        {
            return NoImpact(message, "الحدث لا يحمل سطور قيد.");
        }

        var journal = Step6Journals.TryGetValue(message.EventType, out var known) ? known : JournalType.Cash;
        if (message.EventType == "CashPaymentPosted" && posting.Lines.Any(x => x.AccountRole == R.Expense))
        {
            journal = JournalType.Expense;
        }

        var key = KeyOf(message.EventType, posting.SourceType, posting.SourceId);
        if (message.EventType == "CodCollected" || posting.IsReversal)
        {
            // The same COD can be collected (and its counter receipt cancelled) again after a reversal: one entry per
            // event. The event itself is processed once (unique EventId), and an entry can be reversed once only.
            key += ":" + message.EventId.ToString("N");
        }

        return new PostingPlan
        {
            EventType = message.EventType,
            SourceType = posting.SourceType,
            SourceId = posting.SourceId,
            SourceNumber = posting.SourceNumber,
            SourceKey = key,
            JournalType = posting.IsReversal ? JournalType.Reversal : journal,
            PostingDateUtc = posting.PostingDateUtc == default ? message.OccurredAtUtc : posting.PostingDateUtc,
            CurrencyId = posting.CurrencyId,
            Description = posting.Description,
            IsReversal = posting.IsReversal,
            ReverseLatestOfSource = posting.IsReversal,
            Lines = posting.Lines
        };
    }

    // ------------------------------------------------------------------ receivables (step 5 events)

    private async Task<PostingPlan> InvoiceIssuedAsync(InvoiceIssuedEvent e, CancellationToken cancellationToken)
    {
        var (shipping, commission, services) = await SplitInvoiceRevenueAsync(e.InvoiceId, e.CurrencyId, e.TaxableAmount, cancellationToken);

        return new PostingPlan
        {
            EventType = e.EventType,
            SourceType = "Invoice",
            SourceId = e.InvoiceId,
            SourceNumber = e.InvoiceNumber,
            SourceKey = KeyOf(e.EventType, "Invoice", e.InvoiceId),
            JournalType = JournalType.Sales,
            PostingDateUtc = e.InvoiceDateUtc == default ? e.OccurredAtUtc : e.InvoiceDateUtc,
            CurrencyId = e.CurrencyId,
            Description = $"إصدار الفاتورة {e.InvoiceNumber}",
            BranchId = e.BranchId,
            Lines = Lines(
                Line(R.AccountsReceivable, e.TotalAmount, 0m, e.BranchId, e.CustomerId),
                Line(R.ShippingRevenue, 0m, shipping, e.BranchId, e.CustomerId),
                Line(R.CodCommissionRevenue, 0m, commission, e.BranchId, e.CustomerId),
                Line(R.AdditionalServiceRevenue, 0m, services, e.BranchId, e.CustomerId),
                Line(R.TaxPayable, 0m, e.TaxAmount, e.BranchId, null))
        };
    }

    private async Task<PostingPlan> InvoiceCancelledAsync(InvoiceCancelledEvent e, CancellationToken cancellationToken)
    {
        // Fallback lines when the invoice entry cannot be found (normally the exact reversal is used).
        var (shipping, commission, services) = await SplitInvoiceRevenueAsync(e.InvoiceId, e.CurrencyId, e.TaxableAmount, cancellationToken);

        return new PostingPlan
        {
            EventType = e.EventType,
            SourceType = "Invoice",
            SourceId = e.InvoiceId,
            SourceNumber = e.InvoiceNumber,
            SourceKey = KeyOf(e.EventType, "Invoice", e.InvoiceId),
            JournalType = JournalType.Reversal,
            PostingDateUtc = e.OccurredAtUtc,
            CurrencyId = e.CurrencyId,
            Description = $"إلغاء الفاتورة {e.InvoiceNumber}: {e.Reason}",
            IsReversal = true,
            ReverseSourceKey = KeyOf("InvoiceIssued", "Invoice", e.InvoiceId),
            BranchId = e.BranchId,
            Lines = Lines(
                Line(R.ShippingRevenue, shipping, 0m, e.BranchId, e.CustomerId),
                Line(R.CodCommissionRevenue, commission, 0m, e.BranchId, e.CustomerId),
                Line(R.AdditionalServiceRevenue, services, 0m, e.BranchId, e.CustomerId),
                Line(R.TaxPayable, e.TaxAmount, 0m, e.BranchId, null),
                Line(R.AccountsReceivable, 0m, e.TotalAmount, e.BranchId, e.CustomerId))
        };
    }

    private async Task<PostingPlan> CreditNoteAsync(CreditNoteIssuedEvent e, CancellationToken cancellationToken)
    {
        // The credit note reduces the revenue in the same proportions as the invoice entry.
        var invoiceKey = KeyOf("InvoiceIssued", "Invoice", e.InvoiceId);
        var revenue = await _dbContext.JournalEntryLines.AsNoTracking()
            .Where(x => x.JournalEntry.SourceKey == invoiceKey &&
                        (x.AccountRole == R.ShippingRevenue || x.AccountRole == R.CodCommissionRevenue || x.AccountRole == R.AdditionalServiceRevenue))
            .GroupBy(x => x.AccountRole)
            .Select(g => new { Role = g.Key, Amount = g.Sum(x => x.Credit) })
            .ToListAsync(cancellationToken);

        var decimals = await DecimalsAsync(e.CurrencyId, cancellationToken);
        var total = revenue.Sum(x => x.Amount);
        decimal commission = 0m, services = 0m;
        if (total > 0m)
        {
            commission = JournalMath.Round(e.TaxableAmount * revenue.Where(x => x.Role == R.CodCommissionRevenue).Sum(x => x.Amount) / total, decimals);
            services = JournalMath.Round(e.TaxableAmount * revenue.Where(x => x.Role == R.AdditionalServiceRevenue).Sum(x => x.Amount) / total, decimals);
        }

        var shipping = e.TaxableAmount - commission - services;

        return new PostingPlan
        {
            EventType = e.EventType,
            SourceType = "CreditNote",
            SourceId = e.CreditNoteId,
            SourceNumber = e.CreditNoteNumber,
            SourceKey = KeyOf(e.EventType, "CreditNote", e.CreditNoteId),
            JournalType = JournalType.Sales,
            PostingDateUtc = e.OccurredAtUtc,
            CurrencyId = e.CurrencyId,
            Description = $"إشعار دائن {e.CreditNoteNumber} على الفاتورة {e.InvoiceNumber}",
            BranchId = e.BranchId,
            Lines = Lines(
                Line(R.ShippingRevenue, shipping, 0m, e.BranchId, e.CustomerId),
                Line(R.CodCommissionRevenue, commission, 0m, e.BranchId, e.CustomerId),
                Line(R.AdditionalServiceRevenue, services, 0m, e.BranchId, e.CustomerId),
                Line(R.TaxPayable, e.TaxAmount, 0m, e.BranchId, null),
                Line(R.AccountsReceivable, 0m, e.TotalAmount, e.BranchId, e.CustomerId))
        };
    }

    private static PostingPlan CustomerPayment(CustomerPaymentReceivedEvent e)
    {
        // Cash waits in "cash clearing" until a cash receipt voucher (type CustomerPayment) puts it in a cashbox.
        var role = e.PaymentMethodKind switch
        {
            nameof(PaymentMethodKind.Cash) => R.CashClearing,
            nameof(PaymentMethodKind.BankTransfer) or nameof(PaymentMethodKind.Card) or nameof(PaymentMethodKind.Wallet) => R.Bank,
            _ => R.Suspense
        };

        return new PostingPlan
        {
            EventType = e.EventType,
            SourceType = "CustomerPayment",
            SourceId = e.PaymentId,
            SourceNumber = e.PaymentNumber,
            SourceKey = KeyOf(e.EventType, "CustomerPayment", e.PaymentId),
            JournalType = JournalType.CustomerReceipts,
            PostingDateUtc = e.PaymentDateUtc == default ? e.OccurredAtUtc : e.PaymentDateUtc,
            CurrencyId = e.CurrencyId,
            Description = $"دفعة العميل {e.PaymentNumber}" + (string.IsNullOrWhiteSpace(e.ReferenceNumber) ? string.Empty : $" - مرجع {e.ReferenceNumber}"),
            BranchId = e.BranchId,
            Lines = Lines(
                Line(role, e.Amount, 0m, e.BranchId, e.CustomerId),
                Line(R.AccountsReceivable, 0m, e.Amount, e.BranchId, e.CustomerId))
        };
    }

    /// <summary>
    /// Splits the taxable amount of an invoice into shipping, COD commission and additional services with the pricing
    /// snapshots of its lines (contract and invoice discounts spread proportionally). Rounding goes to shipping.
    /// </summary>
    private async Task<(decimal Shipping, decimal Commission, decimal Services)> SplitInvoiceRevenueAsync(
        long invoiceId, long currencyId, decimal taxableAmount, CancellationToken cancellationToken)
    {
        var lines = await _dbContext.InvoiceLines.AsNoTracking()
            .Where(x => x.InvoiceId == invoiceId)
            .Select(x => new
            {
                x.TaxableAmount,
                SnapshotTaxable = x.PricingSnapshot.TaxableAmount,
                x.PricingSnapshot.CodFee,
                x.PricingSnapshot.ServiceFeesTotal,
                x.PricingSnapshot.ShippingSubtotal,
                x.PricingSnapshot.TotalShippingCharge
            })
            .ToListAsync(cancellationToken);

        decimal commission = 0m, services = 0m;
        foreach (var line in lines)
        {
            if (line.SnapshotTaxable <= 0m || line.TaxableAmount <= 0m)
            {
                continue;
            }

            var factor = line.TaxableAmount / line.SnapshotTaxable;
            commission += Math.Max(0m, line.CodFee) * factor;
            if (line.ShippingSubtotal > 0m)
            {
                services += Math.Max(0m, line.ServiceFeesTotal) * (line.TotalShippingCharge / line.ShippingSubtotal) * factor;
            }
        }

        var decimals = await DecimalsAsync(currencyId, cancellationToken);
        commission = JournalMath.Round(commission, decimals);
        services = JournalMath.Round(services, decimals);

        if (commission < 0m || services < 0m || commission + services > taxableAmount)
        {
            return (taxableAmount, 0m, 0m);
        }

        return (taxableAmount - commission - services, commission, services);
    }

    private async Task<int> DecimalsAsync(long currencyId, CancellationToken cancellationToken) =>
        await _dbContext.Currencies.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.Id == currencyId).Select(x => (int?)x.DecimalPlaces).FirstOrDefaultAsync(cancellationToken) ?? 2;

    // ------------------------------------------------------------------ helpers

    private static T Read<T>(OutboxMessage message) =>
        JsonSerializer.Deserialize<T>(message.PayloadJson, OutboxIntegrationEventPublisher.JsonOptions)
        ?? throw new BusinessRuleException($"تعذّرت قراءة الحدث {message.EventType}.");

    private static PostingPlan NoImpact(OutboxMessage message, string reason) => new()
    {
        EventType = message.EventType,
        PostingDateUtc = message.OccurredAtUtc,
        NoImpactReason = reason
    };

    private static PostingLine Line(string role, decimal debit, decimal credit, long? branchId, long? customerId) =>
        new() { AccountRole = role, Debit = debit, Credit = credit, BranchId = branchId, CustomerId = customerId };

    /// <summary>Drops zero lines (an invoice without tax or commission).</summary>
    private static IReadOnlyList<PostingLine> Lines(params PostingLine[] lines) =>
        lines.Where(x => x.Debit != 0m || x.Credit != 0m).ToList();
}
