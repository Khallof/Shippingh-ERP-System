using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.Identity;

/// <summary>
/// One permission record, e.g. Code = "Shipments.Create".
/// Rows are created by the seeder from Authorization/Permissions.cs; codes are never typed by users.
/// </summary>
public class Permission : BaseEntity
{
    /// <summary>Unique code used in [HasPermission("...")] and in the JWT "permission" claim.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Module the permission belongs to, e.g. "Shipments". Used to group checkboxes in the UI.</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>Arabic description shown in the role editor.</summary>
    public string Description { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
