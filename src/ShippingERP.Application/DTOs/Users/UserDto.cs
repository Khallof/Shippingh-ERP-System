namespace ShippingERP.Application.DTOs.Users;

/// <summary>A user as returned by the users endpoints. Never contains the password hash or security stamp.</summary>
public sealed class UserDto
{
    public long Id { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string ArabicFullName { get; init; } = string.Empty;

    public long? BranchId { get; init; }

    public string? EmployeeCode { get; init; }

    public bool IsActive { get; init; }

    /// <summary>True while the account is locked after too many wrong passwords.</summary>
    public bool IsLockedOut { get; init; }

    public DateTime? LockoutEndUtc { get; init; }

    public DateTime? LastLoginAtUtc { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

    public DateTime CreatedAtUtc { get; init; }

    public long? CreatedByUserId { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }

    public long? UpdatedByUserId { get; init; }
}
