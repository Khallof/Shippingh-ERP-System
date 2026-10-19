using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Shipments;

/// <summary>
/// Warehouse movement manifest (بيان حركة مستودع), number WHM-2026-000001.
/// A batch of shipments scanned INTO a warehouse (CheckIn -> InWarehouse) or OUT of it
/// (CheckOut -> ReceivedAtBranch). Items can be added/removed while Draft; Confirm applies all status
/// changes in one transaction.
/// </summary>
public class ShipmentManifest : BaseEntity
{
    public string ManifestNumber { get; set; } = string.Empty;

    public WarehouseMovementType MovementType { get; set; }

    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public long WarehouseId { get; set; }

    public Warehouse Warehouse { get; set; } = null!;

    public WarehouseManifestStatus Status { get; set; } = WarehouseManifestStatus.Draft;

    public DateTime? ConfirmedAtUtc { get; set; }

    public long? ConfirmedByUserId { get; set; }

    public string? Notes { get; set; }

    public ICollection<ShipmentManifestItem> Items { get; set; } = new List<ShipmentManifestItem>();
}

/// <summary>One shipment on a warehouse movement manifest.</summary>
public class ShipmentManifestItem : BaseEntity
{
    public long ManifestId { get; set; }

    public ShipmentManifest Manifest { get; set; } = null!;

    public long ShipmentId { get; set; }

    public Shipment Shipment { get; set; } = null!;

    /// <summary>The shipment status before the manifest was confirmed.</summary>
    public ShipmentStatus? PreviousStatus { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }
}
