namespace ShippingERP.Application.DTOs.Auth;

/// <summary>The signed-in user, as the frontend needs it (name in the header, menus by permission).</summary>
public sealed class CurrentUserDto
{
    public long Id { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string ArabicFullName { get; init; } = string.Empty;

    public long? BranchId { get; init; }

    public string? EmployeeCode { get; init; }

    public DateTime? LastLoginAtUtc { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
}
