namespace ShippingERP.Application.DTOs.Users;

/// <summary>
/// PUT /api/v1/users/{id}. Replaces the editable fields and the role list.
/// The user name and password cannot be changed here; activation has its own endpoints.
/// </summary>
public sealed class UpdateUserRequest
{
    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string ArabicFullName { get; set; } = string.Empty;

    public long? BranchId { get; set; }

    public string? EmployeeCode { get; set; }

    public List<string> Roles { get; set; } = new();
}
