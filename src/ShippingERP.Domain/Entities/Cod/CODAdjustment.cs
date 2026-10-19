using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.HR;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Cod;

/// <summary>
/// A correction of COD / driver cash (تسوية), number CAD-2026-000001: shortage write-off, excess cash, wrong
/// collected amount, collection on the wrong shipment, extra fee or compensation for the sender.
///
/// Requested with a reason (PendingApproval); nothing changes until ANOTHER user approves it. The approval
/// applies the change, its sub-ledger rows, history and accounting event in one transaction. Never deleted.
/// </summary>
public class CODAdjustment : BaseEntity
{
    public string AdjustmentNumber { get; set; } = string.Empty;

    public CODAdjustmentType AdjustmentType { get; set; }

    public CODAdjustmentStatus Status { get; set; } = CODAdjustmentStatus.PendingApproval;

    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public long CurrencyId { get; set; }

    public Currency Currency { get; set; } = null!;

    /// <summary>Driver adjustments (and collection corrections: the collecting driver).</summary>
    public long? DriverId { get; set; }

    public DriverProfile? Driver { get; set; }

    public long? CODTransactionId { get; set; }

    public CODTransaction? CODTransaction { get; set; }

    /// <summary>Always positive. For CollectionCorrection: |new - previous|.</summary>
    public decimal Amount { get; set; }

    /// <summary>CollectionCorrection only.</summary>
    public decimal? PreviousCollectedAmount { get; set; }

    /// <summary>CollectionCorrection only.</summary>
    public decimal? NewCollectedAmount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public long? DecidedByUserId { get; set; }

    public DateTime? DecidedAtUtc { get; set; }

    public string? DecisionNotes { get; set; }
}
