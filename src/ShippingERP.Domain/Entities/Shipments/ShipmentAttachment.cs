using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Shipments;

/// <summary>
/// A file attached to a shipment (photo, document, signature, damage evidence).
/// The file itself is in the file storage under a GENERATED name (StorageKey). The original name is kept only
/// for display; the physical path is never returned by the API. Deleting is a soft delete (the file stays).
/// </summary>
public class ShipmentAttachment : SoftDeletableEntity
{
    public long ShipmentId { get; set; }

    public Shipment Shipment { get; set; } = null!;

    public ShipmentAttachmentType AttachmentType { get; set; }

    /// <summary>Set when the file is evidence of a damage / loss case.</summary>
    public long? DamageCaseId { get; set; }

    public ShipmentDamageOrLossCase? DamageCase { get; set; }

    /// <summary>File name as uploaded (sanitized), for display only.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Relative key in the file storage, e.g. "shipments/2026/10/3f2a....jpg". Never exposed.</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>Detected from the file content, not from the client.</summary>
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>SHA-256 of the content (hex), to detect tampering and duplicates.</summary>
    public string Sha256 { get; set; } = string.Empty;

    public string? Description { get; set; }
}
