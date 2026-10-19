namespace ShippingERP.Domain.Common;

/// <summary>
/// Base class for master/reference data: branches, cities, customers, pricing zones, vehicles, etc.
///
/// IsActive  = a business switch ("this branch is disabled"). Inactive rows are still visible in lists
///             and reports, they just cannot be selected for new operations.
/// IsDeleted = soft delete ("this row was entered by mistake"). Deleted rows are hidden everywhere
///             by a global query filter, but stay in the database for history and audit.
/// </summary>
public abstract class SoftDeletableEntity : BaseEntity, ISoftDeletable
{
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public long? DeletedByUserId { get; set; }
}
