namespace ShippingERP.Application.DTOs.Roles;

/// <summary>A role with its permission codes.</summary>
public sealed class RoleDto
{
    public long Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ArabicName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsSystemRole { get; init; }

    public bool IsActive { get; init; }

    /// <summary>Number of active users that have this role.</summary>
    public int UserCount { get; init; }

    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }
}
