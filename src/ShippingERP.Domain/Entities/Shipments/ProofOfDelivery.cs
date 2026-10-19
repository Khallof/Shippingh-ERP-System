using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.HR;

namespace ShippingERP.Domain.Entities.Shipments;

/// <summary>
/// Proof of delivery (إثبات التسليم): who received the shipment, when, optional signature and photo files.
/// Append-only. If a delivery is later corrected, the record stays for history.
/// </summary>
public class ProofOfDelivery : BaseEntity, IAppendOnlyEntity
{
    public long ShipmentId { get; set; }

    public Shipment Shipment { get; set; } = null!;

    /// <summary>The driver who delivered (null for a counter pickup by the recipient).</summary>
    public long? DriverId { get; set; }

    public DriverProfile? Driver { get; set; }

    public long? DriverManifestId { get; set; }

    public string RecipientName { get; set; } = string.Empty;

    /// <summary>e.g. "المستلم نفسه", "أخ", "حارس البناء".</summary>
    public string? RecipientRelation { get; set; }

    /// <summary>When the shipment was handed over (UTC).</summary>
    public DateTime DeliveredAtUtc { get; set; }

    public long? SignatureAttachmentId { get; set; }

    public ShipmentAttachment? SignatureAttachment { get; set; }

    public long? PhotoAttachmentId { get; set; }

    public ShipmentAttachment? PhotoAttachment { get; set; }

    /// <summary>decimal(9,6).</summary>
    public decimal? Latitude { get; set; }

    /// <summary>decimal(9,6).</summary>
    public decimal? Longitude { get; set; }

    public string? Notes { get; set; }
}
