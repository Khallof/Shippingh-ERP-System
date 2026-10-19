using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.Shipments;

/// <summary>
/// One physical package (طرد) of a shipment. Each package gets its own label;
/// PackageBarcode = tracking number + "-" + sequence (e.g. SE48213907651-002).
/// Packages can be edited only while the shipment is still editable (they are replaced on update).
/// </summary>
public class ShipmentPackage : BaseEntity
{
    public long ShipmentId { get; set; }

    public Shipment Shipment { get; set; } = null!;

    /// <summary>1, 2, 3...</summary>
    public int SequenceNumber { get; set; }

    public string PackageBarcode { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Actual weight in kg (3 decimals).</summary>
    public decimal WeightKg { get; set; }

    public decimal? LengthCm { get; set; }

    public decimal? WidthCm { get; set; }

    public decimal? HeightCm { get; set; }

    /// <summary>L x W x H / divisor (0 when a dimension is missing).</summary>
    public decimal VolumetricWeightKg { get; set; }
}
