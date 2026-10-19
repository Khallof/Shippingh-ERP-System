namespace ShippingERP.Application.DTOs.Users;

/// <summary>POST /api/v1/users</summary>
public sealed class CreateUserRequest
{
    /// <summary>Latin letters, digits and . _ - only (used to sign in).</summary>
    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    /// <summary>Initial password. Hashed by ASP.NET Core Identity; the user should change it after the first sign-in.</summary>
    public string Password { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string ArabicFullName { get; set; } = string.Empty;

    public long? BranchId { get; set; }

    public string? EmployeeCode { get; set; }

    /// <summary>At least one role name, e.g. ["ShippingAgent"].</summary>
    public List<string> Roles { get; set; } = new();
}
