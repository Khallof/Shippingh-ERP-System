using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.HR;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Shipments;

/// <summary>
/// Driver delivery manifest (بيان توزيع), number DDM-2026-000001: the deliveries handed to one driver.
/// Draft -> Dispatched (shipments OutForDelivery, assigned to the driver) -> Closed (every item delivered or failed).
/// A driver only sees his own manifests.
/// </summary>
public class DriverDeliveryManifest : BaseEntity
{
    public string ManifestNumber { get; set; } = string.Empty;

    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public long DriverId { get; set; }

    public DriverProfile Driver { get; set; } = null!;

    public long? VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    /// <summary>The planned delivery day (UTC date at midnight).</summary>
    public DateTime ManifestDateUtc { get; set; }

    public DriverManifestStatus Status { get; set; } = DriverManifestStatus.Draft;

    public DateTime? DispatchedAtUtc { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public string? Notes { get; set; }

    public ICollection<DriverDeliveryManifestItem> Items { get; set; } = new List<DriverDeliveryManifestItem>();
}

/// <summary>One delivery stop of a driver manifest.</summary>
public class DriverDeliveryManifestItem : BaseEntity
{
    public long ManifestId { get; set; }

    public DriverDeliveryManifest Manifest { get; set; } = null!;

    public long ShipmentId { get; set; }

    public Shipment Shipment { get; set; } = null!;

    /// <summary>Stop order on the route (1, 2, 3...).</summary>
    public int Sequence { get; set; }

    public DeliveryResult Result { get; set; } = DeliveryResult.Pending;

    public DateTime? ResultAtUtc { get; set; }

    public string? FailureReason { get; set; }

    public long? ProofOfDeliveryId { get; set; }

    public ProofOfDelivery? ProofOfDelivery { get; set; }
}
