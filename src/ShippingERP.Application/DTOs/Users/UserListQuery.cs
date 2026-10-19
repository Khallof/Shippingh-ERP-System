using ShippingERP.Application.DTOs.Common;

namespace ShippingERP.Application.DTOs.Users;

/// <summary>
/// GET /api/v1/users query string.
/// Search: user name, e-mail, full name, Arabic full name, employee code, phone.
/// SortBy: userName (default), fullName, arabicFullName, email, employeeCode, createdAtUtc, lastLoginAtUtc.
/// </summary>
public sealed class UserListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields =
    {
        "userName", "fullName", "arabicFullName", "email", "employeeCode", "createdAtUtc", "lastLoginAtUtc"
    };

    /// <summary>Only users that have this role.</summary>
    public string? Role { get; set; }

    /// <summary>Only users of this branch (ignored for callers without Branches.ViewAll: they always see their own branch).</summary>
    public long? BranchId { get; set; }

    public bool? IsActive { get; set; }
}
