using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Cash;

/// <summary>
/// Daily closing of a cashbox (إقفال يومي): the opening balance, the day's total in / out, the expected balance
/// and the cash really counted.
///
/// - Counted = expected: Closed.
/// - A difference needs a reason and goes to PendingApproval; a second user approves it (the difference is posted
///   as ClosingShortage / ClosingExcess and becomes Approved) or rejects it (Rejected: count again).
/// - Once a day is closed (anything but Rejected) no other money can move through the cashbox on that day.
/// One active closing per cashbox and day (filtered unique index).
/// </summary>
public class CashboxDailyClosing : BaseEntity
{
    public long CashboxId { get; set; }

    public Cashbox Cashbox { get; set; } = null!;

    /// <summary>The local business day (date only).</summary>
    public DateTime BusinessDate { get; set; }

    public decimal OpeningBalance { get; set; }

    public decimal TotalIn { get; set; }

    public decimal TotalOut { get; set; }

    public decimal ExpectedBalance { get; set; }

    public decimal CountedBalance { get; set; }

    /// <summary>Counted - expected: negative = shortage (عجز), positive = excess (زيادة).</summary>
    public decimal Discrepancy { get; set; }

    public CashboxClosingStatus Status { get; set; }

    public string? Notes { get; set; }

    public string? DiscrepancyReason { get; set; }

    public long? DecidedByUserId { get; set; }

    public DateTime? DecidedAtUtc { get; set; }

    public string? DecisionNotes { get; set; }

    /// <summary>The ClosingShortage / ClosingExcess transaction posted on approval.</summary>
    public long? AdjustmentTransactionId { get; set; }
}
