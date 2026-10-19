using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Shipments;

// ------------------------------------------------------------------ attachments

/// <summary>Form fields of POST /api/v1/shipments/{id}/attachments (sent with the file as multipart/form-data).</summary>
public class UploadShipmentAttachmentRequest
{
    public ShipmentAttachmentType AttachmentType { get; set; } = ShipmentAttachmentType.Document;

    public string? Description { get; set; }

    /// <summary>Link the file to a damage / loss case of the same shipment (evidence).</summary>
    public long? DamageCaseId { get; set; }
}

public sealed class ShipmentAttachmentDto
{
    public long Id { get; init; }
    public long ShipmentId { get; init; }
    public ShipmentAttachmentType AttachmentType { get; init; }
    public long? DamageCaseId { get; init; }
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string? Description { get; init; }
    /// <summary>API path that streams the file (requires the same access as the shipment).</summary>
    public string DownloadUrl { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedByUserId { get; init; }
}

// ------------------------------------------------------------------ delivery

/// <summary>
/// Form fields of POST /api/v1/shipments/{id}/deliver (multipart/form-data with optional "signature" and "photo" files).
/// </summary>
public class DeliverShipmentRequest
{
    /// <summary>Name of the person who received the shipment.</summary>
    public string RecipientName { get; set; } = string.Empty;

    /// <summary>e.g. "المستلم نفسه", "أخ", "حارس البناء".</summary>
    public string? RecipientRelation { get; set; }

    /// <summary>When it was handed over (default now). Cannot be in the future.</summary>
    public DateTime? DeliveredAtUtc { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? Notes { get; set; }
}

/// <summary>Body of POST /api/v1/shipments/{id}/delivery-failed.</summary>
public sealed class RecordDeliveryFailureRequest
{
    /// <summary>e.g. "المستلم غير موجود", "رفض الاستلام", "العنوان غير صحيح".</summary>
    public string Reason { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime? OccurredAtUtc { get; set; }
}

public sealed class ProofOfDeliveryDto
{
    public long Id { get; init; }
    public long ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public long? DriverId { get; init; }
    public string? DriverName { get; init; }
    public long? DriverManifestId { get; init; }
    public string RecipientName { get; init; } = string.Empty;
    public string? RecipientRelation { get; init; }
    public DateTime DeliveredAtUtc { get; init; }
    public long? SignatureAttachmentId { get; init; }
    public string? SignatureUrl { get; init; }
    public long? PhotoAttachmentId { get; init; }
    public string? PhotoUrl { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedByUserId { get; init; }
}
