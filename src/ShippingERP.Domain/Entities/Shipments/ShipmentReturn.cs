using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Shipments;

/// <summary>
/// Return to sender (مرتجع), number RET-2026-000001.
/// Open -> (the shipment travels back to ReturnBranch with normal transfers) -> Completed (status Returned).
/// For a delivered shipment (AfterDelivery) the shipment is first received back at a branch, which is the only
/// allowed way out of "Delivered" besides a status correction.
/// ReturnCharge is stored only (no accounting yet).
/// </summary>
public class ShipmentReturn : BaseEntity
{
    public string ReturnNumber { get; set; } = string.Empty;

    public long ShipmentId { get; set; }

    public Shipment Shipment { get; set; } = null!;

    public ShipmentReturnType ReturnType { get; set; }

    public ShipmentReturnStatus Status { get; set; } = ShipmentReturnStatus.Open;

    public string Reason { get; set; } = string.Empty;

    /// <summary>Where the sender will get it back (default: the origin branch).</summary>
    public long ReturnBranchId { get; set; }

    public Branch ReturnBranch { get; set; } = null!;

    public decimal ReturnCharge { get; set; }

    /// <summary>For AfterDelivery returns: when the shipment came back to a branch.</summary>
    public DateTime? ReceivedBackAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public string? Notes { get; set; }
}
