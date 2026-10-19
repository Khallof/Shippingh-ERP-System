using Microsoft.AspNetCore.Identity;
using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.Identity;

/// <summary>
/// A role (job function) such as Accountant or Driver. A role is a named group of permissions;
/// users get permissions only through their roles.
/// </summary>
public class ApplicationRole : IdentityRole<long>, IAuditableEntity
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName)
        : base(roleName)
    {
    }

    /// <summary>Arabic display name, e.g. "محاسب".</summary>
    public string ArabicName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// True for the seven built-in roles. System roles cannot be renamed; SystemAdmin's permissions
    /// cannot be edited (it always has every permission).
    /// </summary>
    public bool IsSystemRole { get; set; }

    /// <summary>An inactive role grants no permissions.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public long? CreatedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public long? UpdatedByUserId { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
