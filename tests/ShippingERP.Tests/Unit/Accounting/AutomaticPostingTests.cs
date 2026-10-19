using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Application.Interfaces.Billing;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Domain.Entities.Integration;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;
using A = ShippingERP.Tests.Common.AccountingTestData;

namespace ShippingERP.Tests.Unit.Accounting;

/// <summary>
/// Automatic posting of the operational events (step 7): invoices, credit notes, cancellations, customer payments,
/// the COD lifecycle, cash vouchers and transfers; idempotency (redelivery, duplicate events); failures (missing
/// posting rule, closed period) and their retry.
/// </summary>
public sealed class AutomaticPostingTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        _ids = await A.SeedAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private async Task<InvoiceDto> IssuedInvoiceAsync(string sender = "CUS-00003")
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, sender: sender));
        return await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);
    }

    private async Task<JournalEntryLikeResult> SingleEntryAsync(string eventType)
    {
        var entry = Assert.Single(await A.EntriesOfAsync(_context, eventType));
        return new JournalEntryLikeResult(entry.Id, entry.Status, entry.JournalType, entry.SourceNumber, A.NetByAccount(entry));
    }

    private sealed record JournalEntryLikeResult(long Id, JournalEntryStatus Status, JournalType JournalType, string? SourceNumber, Dictionary<string, decimal> Net);

    [Fact]
    public async Task CashboxOpeningBalances_ArePosted_AndTheOutboxIsEmptied()
    {
        var result = await A.ProcessAsync(_context);

        Assert.Equal(0, result.Failed);
        Assert.Equal(0, result.Remaining);
        Assert.True(result.Posted >= 2);
        Assert.Equal(750_000m, await A.BalanceAsync(_context, A.Cashboxes));
        Assert.Equal(-750_000m, await A.BalanceAsync(_context, A.OpeningBalances));
        Assert.Equal(0, await _context.QueryAsync(db => db.OutboxMessages.CountAsync(x => x.ProcessedAtUtc == null)));
        Assert.Empty(await A.UnbalancedEntriesAsync(_context));
    }

    [Fact]
    public async Task InvoiceIssued_DebitsReceivables_CreditsShippingRevenueAndTax()
    {
        await BillingTestData.SetDefaultTaxAsync(_context, "VAT5");
        var invoice = await IssuedInvoiceAsync();

        await A.ProcessAsync(_context);

        var entry = await SingleEntryAsync("InvoiceIssued");
        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
        Assert.Equal(JournalType.Sales, entry.JournalType);
        Assert.Equal(invoice.InvoiceNumber, entry.SourceNumber);
        Assert.Equal(15_750m, entry.Net[A.Receivables]);
        Assert.Equal(-15_000m, entry.Net[A.ShippingRevenue]);
        Assert.Equal(-750m, entry.Net[A.TaxPayable]);
        Assert.Equal(3, entry.Net.Count);
    }

    [Fact]
    public async Task RedeliveredOrDuplicatedEvents_AreNeverPostedTwice()
    {
        await IssuedInvoiceAsync();
        await A.ProcessAsync(_context);
        var second = await A.ProcessAsync(_context);
        Assert.Equal(0, second.Processed);

        // 1. The same outbox message delivered again (its "processed" mark lost).
        await _context.QueryAsync(async db =>
        {
            var message = await db.OutboxMessages.SingleAsync(x => x.EventType == "InvoiceIssued");
            message.ProcessedAtUtc = null;
            return await db.SaveChangesAsync();
        });
        await A.ProcessAsync(_context);

        // 2. The same business event published twice (another EventId, same invoice).
        await _context.QueryAsync(async db =>
        {
            var message = await db.OutboxMessages.AsNoTracking().SingleAsync(x => x.EventType == "InvoiceIssued");
            db.OutboxMessages.Add(new OutboxMessage
            {
                EventId = Guid.NewGuid(),
                EventType = message.EventType,
                PayloadJson = message.PayloadJson,
                OccurredAtUtc = message.OccurredAtUtc
            });
            return await db.SaveChangesAsync();
        });
        var third = await A.ProcessAsync(_context);

        Assert.Equal(1, third.Duplicates);
        Assert.Single(await A.EntriesOfAsync(_context, "InvoiceIssued"));
        Assert.Equal(15_000m, await A.BalanceAsync(_context, A.Receivables));
        var statuses = await _context.QueryAsync(db => db.AccountingPostingEvents.Where(x => x.EventType == "InvoiceIssued").Select(x => x.Status).ToListAsync());
        Assert.Equal(new[] { PostingEventStatus.Duplicate, PostingEventStatus.Posted }, statuses.OrderBy(x => x == PostingEventStatus.Posted).ToArray());
    }

    [Fact]
    public async Task InvoiceCancelled_ReversesTheInvoiceEntryExactly()
    {
        var invoice = await IssuedInvoiceAsync();
        await A.ProcessAsync(_context);

        await _context.UseAsync<IInvoiceService, InvoiceDto>(s => s.CancelAsync(invoice.Id, new CancelInvoiceRequest { Reason = "فاتورة بالخطأ" }, CancellationToken.None));
        await A.ProcessAsync(_context);

        var issued = await SingleEntryAsync("InvoiceIssued");
        var cancelled = Assert.Single(await A.EntriesOfAsync(_context, "InvoiceCancelled"));
        Assert.Equal(JournalEntryStatus.Reversed, issued.Status);
        Assert.Equal(issued.Id, cancelled.ReversalOfEntryId);
        Assert.Equal(JournalType.Reversal, cancelled.JournalType);
        Assert.Equal(0m, await A.BalanceAsync(_context, A.Receivables));
        Assert.Equal(0m, await A.BalanceAsync(_context, A.ShippingRevenue));
    }

    [Fact]
    public async Task CreditNote_DebitsRevenueAndTax_CreditsReceivables()
    {
        await BillingTestData.SetDefaultTaxAsync(_context, "VAT5");
        var invoice = await IssuedInvoiceAsync();
        await A.ProcessAsync(_context);

        await _context.UseAsync<IInvoiceService, CreditNoteDto>(s => s.CreateCreditNoteAsync(
            invoice.Id, new CreateCreditNoteRequest { Reason = "تأخر التسليم", CreditAll = true }, CancellationToken.None));
        await A.ProcessAsync(_context);

        var credit = await SingleEntryAsync("CreditNoteIssued");
        Assert.Equal(15_000m, credit.Net[A.ShippingRevenue]);
        Assert.Equal(750m, credit.Net[A.TaxPayable]);
        Assert.Equal(-15_750m, credit.Net[A.Receivables]);
        Assert.Equal(0m, await A.BalanceAsync(_context, A.Receivables));
    }

    [Fact]
    public async Task CustomerPayments_DebitCashClearingOrBank_AndCreditReceivables()
    {
        var invoice = await IssuedInvoiceAsync();
        var customerId = _ids.Customer("CUS-00003");

        await BillingTestData.PayAsync(_context, customerId, 10_000m, "CASH", allocations: new[] { (invoice.Id, 10_000m) });
        await BillingTestData.PayAsync(_context, customerId, 5_000m, "BANK", allocations: new[] { (invoice.Id, 5_000m) });
        await A.ProcessAsync(_context);

        // Cash waits in "customer cash awaiting deposit" until a cash receipt voucher puts it in a cashbox.
        Assert.Equal(10_000m, await A.BalanceAsync(_context, A.CashClearing));
        Assert.Equal(5_000m, await A.BalanceAsync(_context, A.Banks));
        Assert.Equal(0m, await A.BalanceAsync(_context, A.Receivables));
        Assert.All(await A.EntriesOfAsync(_context, "CustomerPaymentReceived"), x => Assert.Equal(JournalType.CustomerReceipts, x.JournalType));
    }

    [Fact]
    public async Task CodLifecycle_KeepsSenderMoneyOutOfRevenue_UntilTheSettlementRecognizesTheCharges()
    {
        var collected = await CodTestData.CollectedCodAsync(_context, _ids);
        await A.ProcessAsync(_context);

        var collection = await SingleEntryAsync("CodCollected");
        Assert.Equal(CodTestData.Cod, collection.Net[A.DriverCustody]);
        Assert.Equal(-CodTestData.Cod, collection.Net[A.SenderCod]);
        Assert.Equal(0m, await A.BalanceAsync(_context, A.ShippingRevenue));

        await CodTestData.HandoverAsync(_context, _ids.DamascusDriver, CodTestData.Cod, codIds: new[] { collected.Id });
        await A.ProcessAsync(_context);

        var handover = await SingleEntryAsync("DriverCashHandedOver");
        Assert.Equal(CodTestData.Cod, handover.Net[A.Cashboxes]);
        Assert.Equal(-CodTestData.Cod, handover.Net[A.DriverCustody]);
        Assert.Equal(0m, await A.BalanceAsync(_context, A.DriverCustody));

        await CodTestData.MarkReadyAsync(_context, collected.Id);
        var settlement = await CodTestData.CreateSettlementAsync(_context, collected.SenderCustomerId, new Application.DTOs.Cod.SettlementLineRequest { CodTransactionId = collected.Id });
        await CodTestData.ApproveSettlementAsync(_context, settlement.Id);
        await CodTestData.PayCashAsync(_context, settlement.Id);
        await A.ProcessAsync(_context);

        var paid = await SingleEntryAsync("SenderSettlementPaid");
        Assert.Equal(JournalType.SenderSettlement, paid.JournalType);
        Assert.Equal(CodTestData.Cod, paid.Net[A.SenderCod]);
        Assert.Equal(-CodTestData.Net, paid.Net[A.Cashboxes]);
        Assert.Equal(-15_000m, paid.Net[A.ShippingRevenue]);
        Assert.Equal(-3_000m, paid.Net[A.CodCommission]);

        Assert.Equal(0m, await A.BalanceAsync(_context, A.SenderCod));
        Assert.Equal(-15_000m, await A.BalanceAsync(_context, A.ShippingRevenue));
        Assert.Equal(-3_000m, await A.BalanceAsync(_context, A.CodCommission));
        Assert.Equal(750_000m + CodTestData.Cod - CodTestData.Net, await A.BalanceAsync(_context, A.Cashboxes));
        Assert.Empty(await A.UnbalancedEntriesAsync(_context));
    }

    [Fact]
    public async Task ExpenseVoucher_UsesTheExpenseCategoryRule_AndItsCancellationReversesTheEntry()
    {
        var cashboxId = await CodTestData.CashboxIdAsync(_context);
        var voucher = await CodTestData.PayExpenseAsync(_context, cashboxId, 25_000m);
        await A.ProcessAsync(_context);

        var expense = await SingleEntryAsync("CashPaymentPosted");
        Assert.Equal(JournalType.Expense, expense.JournalType);
        Assert.Equal(25_000m, expense.Net[A.Fuel]);
        Assert.Equal(-25_000m, expense.Net[A.Cashboxes]);
        Assert.Equal(0m, await A.BalanceAsync(_context, A.OtherExpenses));

        await _context.UseAsync<ICashVoucherService, CashPaymentVoucherDto>(s => s.CancelPaymentAsync(
            voucher.Voucher.Id, new CancelCashVoucherRequest { Reason = "سند مكرر" }, CancellationToken.None));
        await A.ProcessAsync(_context);

        var reversal = Assert.Single(await A.EntriesOfAsync(_context, "CashVoucherCancelled"));
        Assert.Equal(expense.Id, reversal.ReversalOfEntryId);
        Assert.Equal(JournalEntryStatus.Reversed, (await SingleEntryAsync("CashPaymentPosted")).Status);
        Assert.Equal(0m, await A.BalanceAsync(_context, A.Fuel));
    }

    [Fact]
    public async Task TransferBetweenBranches_GoesThroughCashInTransit()
    {
        var from = await CodTestData.CashboxIdAsync(_context);
        var to = await CodTestData.CashboxIdAsync(_context, "ALP-SYP-01");

        var transfer = await _context.UseAsync<ICashTransferService, CashTransferDto>(s => s.CreateAsync(
            new CreateCashTransferRequest { FromCashboxId = from, ToCashboxId = to, Amount = 100_000m, Reason = "تغذية صندوق حلب" }, CancellationToken.None));
        await _context.UseAsync<ICashTransferService, CashTransferDto>(s => s.ApproveAsync(transfer.Id, new ApproveCashTransferRequest(), CancellationToken.None));
        await A.ProcessAsync(_context);

        Assert.Equal(100_000m, await A.BalanceAsync(_context, "1102"));

        await _context.UseAsync<ICashTransferService, CashTransferDto>(s => s.ReceiveAsync(transfer.Id, CancellationToken.None));
        await A.ProcessAsync(_context);

        Assert.Equal(0m, await A.BalanceAsync(_context, "1102"));
        Assert.Equal(750_000m, await A.BalanceAsync(_context, A.Cashboxes));
    }

    [Fact]
    public async Task MissingPostingRule_FailsTheEvent_AndARetryPostsItOnceFixed()
    {
        await A.ProcessAsync(_context);
        await SetRuleActiveAsync("OtherIncome", false);

        var cashboxId = await CodTestData.CashboxIdAsync(_context);
        await _context.UseAsync<ICashVoucherService, CashVoucherCreationResult<CashReceiptVoucherDto>>(s => s.CreateReceiptAsync(new CreateCashReceiptRequest
        {
            CashboxId = cashboxId, ReceiptType = CashReceiptType.OtherIncome, Amount = 40_000m,
            ReceivedFrom = "مستأجر المستودع", Description = "إيجار ركن في المستودع"
        }, null, CancellationToken.None));

        var result = await A.ProcessAsync(_context);
        Assert.Equal(1, result.Failed);

        var failed = await _context.QueryAsync(db => db.AccountingPostingEvents.SingleAsync(x => x.EventType == "CashReceiptPosted"));
        Assert.Equal(PostingEventStatus.Failed, failed.Status);
        Assert.Contains("OtherIncome", failed.LastError);
        Assert.Null(failed.JournalEntryId);
        Assert.Empty(await A.EntriesOfAsync(_context, "CashReceiptPosted"));
        var message = await _context.QueryAsync(db => db.OutboxMessages.SingleAsync(x => x.EventType == "CashReceiptPosted"));
        Assert.Null(message.ProcessedAtUtc);
        Assert.Equal(1, message.Attempts);

        await SetRuleActiveAsync("OtherIncome", true);
        var retried = await _context.UseAsync<IAccountingPostingService, PostingEventDto>(s => s.RetryAsync(failed.Id, new RetryPostingEventRequest(), CancellationToken.None));

        Assert.Equal(PostingEventStatus.Posted, retried.Status);
        Assert.NotNull(retried.JournalEntryId);
        Assert.Equal(-40_000m, await A.BalanceAsync(_context, A.OtherIncome));
    }

    [Fact]
    public async Task EventInAClosedPeriod_Fails_AndIsPostedOnAnOpenDateByTheRetry()
    {
        var today = await A.TodayAsync(_context);
        await A.NextFiscalYearAsync(_context);
        await A.ProcessAsync(_context);
        var period = await A.CurrentPeriodAsync(_context, today);
        await A.ClosePeriodAsync(_context, period.Id);

        var cashboxId = await CodTestData.CashboxIdAsync(_context);
        await CodTestData.PayExpenseAsync(_context, cashboxId, 9_000m);
        var result = await A.ProcessAsync(_context);

        Assert.Equal(1, result.Failed);
        var failed = await _context.QueryAsync(db => db.AccountingPostingEvents.SingleAsync(x => x.EventType == "CashPaymentPosted"));
        Assert.Contains("مقفلة", failed.LastError);
        Assert.Equal(0m, await A.BalanceAsync(_context, A.Fuel));

        var nextMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        var retried = await _context.UseAsync<IAccountingPostingService, PostingEventDto>(s =>
            s.RetryAsync(failed.Id, new RetryPostingEventRequest { PostingDate = nextMonth }, CancellationToken.None));

        Assert.Equal(PostingEventStatus.Posted, retried.Status);
        var entry = await A.GetAsync(_context, retried.JournalEntryId!.Value);
        Assert.Equal(nextMonth, entry.EntryDate);
        Assert.Contains(today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), entry.Description);
        Assert.Equal(9_000m, await A.BalanceAsync(_context, A.Fuel));
    }

    [Fact]
    public async Task PaymentAllocation_HasNoAccountingEffect()
    {
        var invoice = await IssuedInvoiceAsync();
        var payment = await BillingTestData.PayAsync(_context, _ids.Customer("CUS-00003"), 15_000m, "CASH");
        await _context.UseAsync<ICustomerPaymentService, CustomerPaymentDto>(s => s.AllocateAsync(
            payment.Payment.Id, new AllocatePaymentRequest { Allocations = new List<PaymentAllocationRequest> { new() { InvoiceId = invoice.Id, Amount = 15_000m } } },
            CancellationToken.None));

        var result = await A.ProcessAsync(_context);

        Assert.True(result.Skipped >= 1);
        var allocated = await _context.QueryAsync(db => db.AccountingPostingEvents.SingleAsync(x => x.EventType == "CustomerPaymentAllocated"));
        Assert.Equal(PostingEventStatus.Skipped, allocated.Status);
        Assert.Null(allocated.JournalEntryId);
        Assert.Equal(0m, await A.BalanceAsync(_context, A.Receivables));
    }

    private Task<int> SetRuleActiveAsync(string role, bool active) =>
        _context.QueryAsync(async db =>
        {
            foreach (var rule in await db.AccountingPostingRules.Where(x => x.AccountRole == role).ToListAsync())
            {
                rule.IsActive = active;
            }

            return await db.SaveChangesAsync();
        });
}
