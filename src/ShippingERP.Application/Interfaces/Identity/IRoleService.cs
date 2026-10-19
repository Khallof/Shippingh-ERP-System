using ShippingERP.Application.DTOs.Roles;

namespace ShippingERP.Application.Interfaces.Identity;

/// <summary>Roles and their permissions (Roles.Manage).</summary>
public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken);

    Task<RoleDto> GetRoleAsync(long id, CancellationToken cancellationToken);

    IReadOnlyList<PermissionDto> GetPermissionCatalog();

    Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken);

    Task<RoleDto> UpdateRolePermissionsAsync(long id, UpdateRolePermissionsRequest request, CancellationToken cancellationToken);
}
