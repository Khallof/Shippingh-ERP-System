using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.HR;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Shipments;

/// <summary>
/// Branch transfer manifest (بيان تحويل بين الفروع), number BTR-2026-000001.
/// Draft (add shipments of the origin branch) -> Dispatched (shipments InTransit) ->
/// Received / PartiallyReceived (shipments arrive at ToBranch; missing ones are flagged).
/// </summary>
public class BranchTransfer : BaseEntity
{
    public string TransferNumber { get; set; } = string.Empty;

    public long FromBranchId { get; set; }

    public Branch FromBranch { get; set; } = null!;

    public long ToBranchId { get; set; }

    public Branch ToBranch { get; set; } = null!;

    /// <summary>Line-haul vehicle (optional).</summary>
    public long? VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    /// <summary>Line-haul driver (optional).</summary>
    public long? DriverId { get; set; }

    public DriverProfile? Driver { get; set; }

    /// <summary>Seal number of the truck or bag (optional).</summary>
    public string? SealNumber { get; set; }

    public BranchTransferStatus Status { get; set; } = BranchTransferStatus.Draft;

    public DateTime? DispatchedAtUtc { get; set; }

    public long? DispatchedByUserId { get; set; }

    public DateTime? ReceivedAtUtc { get; set; }

    public long? ReceivedByUserId { get; set; }

    public string? Notes { get; set; }

    public ICollection<BranchTransferItem> Items { get; set; } = new List<BranchTransferItem>();
}

/// <summary>One shipment on a branch transfer.</summary>
public class BranchTransferItem : BaseEntity
{
    public long BranchTransferId { get; set; }

    public BranchTransfer BranchTransfer { get; set; } = null!;

    public long ShipmentId { get; set; }

    public Shipment Shipment { get; set; } = null!;

    public BranchTransferItemStatus ItemStatus { get; set; } = BranchTransferItemStatus.Pending;

    public DateTime? ReceivedAtUtc { get; set; }

    public string? Notes { get; set; }
}
