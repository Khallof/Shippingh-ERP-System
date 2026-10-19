namespace ShippingERP.Domain.Common;

/// <summary>
/// Base class for every database entity.
/// Audit fields are filled automatically by the Infrastructure layer (AuditableEntityInterceptor),
/// so business code must never set them manually.
/// All dates are stored in UTC.
/// </summary>
public abstract class BaseEntity : IAuditableEntity
{
    /// <summary>Primary key (SQL Server IDENTITY bigint).</summary>
    public long Id { get; set; }

    /// <summary>When the record was created (UTC).</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>The user who created the record. Null for system-generated records.</summary>
    public long? CreatedByUserId { get; set; }

    /// <summary>When the record was last updated (UTC). Null if never updated.</summary>
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>The user who last updated the record.</summary>
    public long? UpdatedByUserId { get; set; }

    /// <summary>
    /// Optimistic concurrency token (SQL Server rowversion).
    /// Prevents two users from silently overwriting each other's changes.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
