namespace ShippingERP.Domain.Common;

/// <summary>
/// The four audit columns every table carries. BaseEntity implements it, and so do the Identity
/// entities (ApplicationUser, ApplicationRole) that cannot inherit BaseEntity because they already
/// inherit IdentityUser / IdentityRole.
///
/// The AuditableEntityInterceptor fills these values automatically; business code never sets them.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; set; }

    long? CreatedByUserId { get; set; }

    DateTime? UpdatedAtUtc { get; set; }

    long? UpdatedByUserId { get; set; }
}
