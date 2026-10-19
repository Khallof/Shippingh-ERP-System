using ShippingERP.Domain.Entities.Billing;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Billing;

/// <summary>
/// Invoice state machine.
///
/// User actions: issue (Draft -> Issued) and cancel (Draft, or an open invoice without payments and credit notes
/// -> Cancelled). Every other status is DERIVED from the amounts by <see cref="Refresh"/> after a payment, a credit
/// note or the daily overdue check:
///
///   credit notes >= total               -> Credited
///   balance = 0                         -> Paid
///   balance > 0 and due date passed     -> Overdue
///   something paid or credited          -> PartiallyPaid
///   otherwise                           -> Issued
///
/// Allowed transitions:
///   Draft          -> Issued, Cancelled
///   Issued         -> PartiallyPaid, Paid, Overdue, Credited, Cancelled
///   PartiallyPaid  -> Paid, Overdue, Credited
///   Overdue        -> PartiallyPaid, Paid, Credited, Cancelled
///   Paid           -> Credited
///   Cancelled, Credited: final
/// </summary>
public static class InvoiceStatusRules
{
    private static readonly IReadOnlyDictionary<InvoiceStatus, InvoiceStatus[]> Transitions = new Dictionary<InvoiceStatus, InvoiceStatus[]>
    {
        [InvoiceStatus.Draft] = new[] { InvoiceStatus.Issued, InvoiceStatus.Cancelled },
        [InvoiceStatus.Issued] = new[] { InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid, InvoiceStatus.Overdue, InvoiceStatus.Credited, InvoiceStatus.Cancelled },
        [InvoiceStatus.PartiallyPaid] = new[] { InvoiceStatus.Paid, InvoiceStatus.Overdue, InvoiceStatus.Credited },
        [InvoiceStatus.Overdue] = new[] { InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid, InvoiceStatus.Credited, InvoiceStatus.Cancelled },
        [InvoiceStatus.Paid] = new[] { InvoiceStatus.Credited },
        [InvoiceStatus.Cancelled] = Array.Empty<InvoiceStatus>(),
        [InvoiceStatus.Credited] = Array.Empty<InvoiceStatus>()
    };

    /// <summary>Statuses that still expect money (payments can be allocated, the aging report shows them).</summary>
    public static readonly IReadOnlySet<InvoiceStatus> OpenStatuses =
        new HashSet<InvoiceStatus> { InvoiceStatus.Issued, InvoiceStatus.PartiallyPaid, InvoiceStatus.Overdue };

    /// <summary>Statuses of an invoice that was issued and not cancelled (it is part of the customer balance).</summary>
    public static readonly IReadOnlySet<InvoiceStatus> PostedStatuses =
        new HashSet<InvoiceStatus> { InvoiceStatus.Issued, InvoiceStatus.PartiallyPaid, InvoiceStatus.Overdue, InvoiceStatus.Paid, InvoiceStatus.Credited };

    public static IReadOnlyList<InvoiceStatus> NextStatuses(InvoiceStatus status) =>
        Transitions.TryGetValue(status, out var next) ? next : Array.Empty<InvoiceStatus>();

    public static bool CanTransition(InvoiceStatus from, InvoiceStatus to) => from == to || NextStatuses(from).Contains(to);

    public static bool IsOpen(InvoiceStatus status) => OpenStatuses.Contains(status);

    public static bool CanIssue(InvoiceStatus status) => status == InvoiceStatus.Draft;

    /// <summary>Only issued invoices can be credited; a fully credited invoice cannot be credited again.</summary>
    public static bool CanCredit(InvoiceStatus status) => IsOpen(status) || status == InvoiceStatus.Paid;

    /// <summary>A draft, or an open invoice that has no payment allocation and no credit note.</summary>
    public static bool CanCancel(Invoice invoice) =>
        invoice.Status == InvoiceStatus.Draft ||
        ((invoice.Status is InvoiceStatus.Issued or InvoiceStatus.Overdue) && invoice.PaidAmount == 0m && invoice.CreditedAmount == 0m);

    /// <summary>The status the amounts and the due date call for (Draft and Cancelled never change here).</summary>
    public static InvoiceStatus Calculate(Invoice invoice, DateTime nowUtc)
    {
        if (invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled)
        {
            return invoice.Status;
        }

        var balance = Balance(invoice);

        if (invoice.TotalAmount > 0m && invoice.CreditedAmount >= invoice.TotalAmount)
        {
            return InvoiceStatus.Credited;
        }

        if (balance <= 0m)
        {
            return InvoiceStatus.Paid;
        }

        if (invoice.DueDateUtc.HasValue && invoice.DueDateUtc.Value < nowUtc)
        {
            return InvoiceStatus.Overdue;
        }

        return invoice.PaidAmount > 0m || invoice.CreditAppliedAmount > 0m
            ? InvoiceStatus.PartiallyPaid
            : InvoiceStatus.Issued;
    }

    /// <summary>TotalAmount - PaidAmount - CreditAppliedAmount (never negative).</summary>
    public static decimal Balance(Invoice invoice) =>
        Math.Max(0m, invoice.TotalAmount - invoice.PaidAmount - invoice.CreditAppliedAmount);

    /// <summary>
    /// Recalculates BalanceDue and the status. Throws when the new status is not reachable from the current one
    /// (a programming error, never a user error). Returns true when the status changed.
    /// </summary>
    public static bool Refresh(Invoice invoice, DateTime nowUtc)
    {
        invoice.BalanceDue = invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled
            ? (invoice.Status == InvoiceStatus.Draft ? invoice.TotalAmount : 0m)
            : Balance(invoice);

        var next = Calculate(invoice, nowUtc);
        if (next == invoice.Status)
        {
            return false;
        }

        if (!CanTransition(invoice.Status, next))
        {
            throw new InvalidOperationException($"Invoice {invoice.Id}: transition {invoice.Status} -> {next} is not allowed.");
        }

        if (next == InvoiceStatus.Paid && invoice.PaidAtUtc is null)
        {
            invoice.PaidAtUtc = nowUtc;
        }

        invoice.Status = next;
        return true;
    }
}

/// <summary>Arabic display names of the billing enums.</summary>
public static class BillingNames
{
    public static string Arabic(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "مسودة",
        InvoiceStatus.Issued => "صادرة",
        InvoiceStatus.PartiallyPaid => "مدفوعة جزئياً",
        InvoiceStatus.Paid => "مدفوعة",
        InvoiceStatus.Overdue => "متأخرة السداد",
        InvoiceStatus.Cancelled => "ملغاة",
        InvoiceStatus.Credited => "مُشعَر بها دائناً بالكامل",
        _ => status.ToString()
    };

    public static string Arabic(CustomerAccountTransactionType type) => type switch
    {
        CustomerAccountTransactionType.InvoiceIssued => "فاتورة",
        CustomerAccountTransactionType.InvoiceCancelled => "إلغاء فاتورة",
        CustomerAccountTransactionType.PaymentReceived => "سند قبض",
        CustomerAccountTransactionType.CreditNoteIssued => "إشعار دائن",
        _ => type.ToString()
    };

    public static string Arabic(PriceOverrideStatus status) => status switch
    {
        PriceOverrideStatus.Pending => "بانتظار الموافقة",
        PriceOverrideStatus.Approved => "معتمد",
        PriceOverrideStatus.Rejected => "مرفوض",
        PriceOverrideStatus.Cancelled => "ملغى",
        _ => status.ToString()
    };
}
