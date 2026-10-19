using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Identity;

namespace ShippingERP.Domain.Entities.HR;

/// <summary>
/// An employee of a branch. May be linked to a system user (ApplicationUser) when the employee signs in.
/// One user can be linked to at most one employee.
/// </summary>
public class Employee : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    public long BranchId { get; set; }

    public MasterData.Branch Branch { get; set; } = null!;

    public string? JobTitle { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? NationalId { get; set; }

    public DateTime? HireDateUtc { get; set; }

    public long? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public string? Notes { get; set; }

    public DriverProfile? DriverProfile { get; set; }
}
