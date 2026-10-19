namespace ShippingERP.Application.DTOs.Roles;

/// <summary>
/// PUT /api/v1/roles/{id}/permissions. The list REPLACES the role's permissions
/// (send the full list; an empty list removes all permissions).
/// </summary>
public sealed class UpdateRolePermissionsRequest
{
    public List<string> Permissions { get; set; } = new();
}
