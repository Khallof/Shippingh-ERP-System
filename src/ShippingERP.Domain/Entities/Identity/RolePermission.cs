using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.Identity;

/// <summary>
/// Link table: which permissions a role has (many-to-many between Roles and Permissions).
/// A role's permission list is replaced as a whole by PUT /api/v1/roles/{id}/permissions.
/// </summary>
public class RolePermission : BaseEntity
{
    public long RoleId { get; set; }

    public ApplicationRole Role { get; set; } = null!;

    public long PermissionId { get; set; }

    public Permission Permission { get; set; } = null!;
}
