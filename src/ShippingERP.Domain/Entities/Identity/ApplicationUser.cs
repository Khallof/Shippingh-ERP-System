using Microsoft.AspNetCore.Identity;
using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.Identity;

/// <summary>
/// A person who signs in to the ERP (employee, driver or customer portal user).
///
/// Inherits ASP.NET Core Identity's user (UserName, Email, PasswordHash, SecurityStamp, lockout...)
/// with a bigint key. Passwords are hashed ONLY by Identity's PasswordHasher (PBKDF2); this class
/// never stores or sees a plain password.
///
/// Users are never deleted: they are deactivated (IsActive = false), so history keeps pointing at them.
/// </summary>
public class ApplicationUser : IdentityUser<long>, IAuditableEntity
{
    /// <summary>Full name in English (or Latin letters), e.g. "Ahmed Ali".</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Full name in Arabic, shown in the RTL interface, e.g. "أحمد علي".</summary>
    public string ArabicFullName { get; set; } = string.Empty;

    /// <summary>
    /// The branch the user works in. Null = head office / not tied to a branch.
    /// The Branches table is created in a later step; the foreign key will be added then.
    /// </summary>
    public long? BranchId { get; set; }

    /// <summary>Optional HR employee number (unique when present).</summary>
    public string? EmployeeCode { get; set; }

    /// <summary>False = the user cannot sign in and all existing sessions stop working.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Last successful sign-in (UTC).</summary>
    public DateTime? LastLoginAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public long? CreatedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public long? UpdatedByUserId { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
