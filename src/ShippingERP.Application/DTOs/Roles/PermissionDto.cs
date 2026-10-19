namespace ShippingERP.Application.DTOs.Roles;

/// <summary>One entry of the permission catalog (GET /api/v1/roles/permissions).</summary>
public sealed class PermissionDto
{
    public string Code { get; init; } = string.Empty;

    public string Module { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}
