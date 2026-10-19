namespace ShippingERP.Domain.Common;

/// <summary>
/// Marks an entity that is never physically removed from the database.
/// When EF Core is asked to delete it, the AuditableEntityInterceptor converts the delete
/// into an update that sets IsDeleted = true. A global query filter hides deleted rows.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTime? DeletedAtUtc { get; set; }

    long? DeletedByUserId { get; set; }
}
