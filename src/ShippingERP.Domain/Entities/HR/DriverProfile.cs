using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Identity;

namespace ShippingERP.Domain.Entities.HR;

/// <summary>
/// Driver details of an employee (one profile per employee). The driver's branch is the employee's branch.
/// UserId links the driver to the account used in the driver mobile app.
/// </summary>
public class DriverProfile : SoftDeletableEntity
{
    public long EmployeeId { get; set; }

    public Employee Employee { get; set; } = null!;

    public long? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public string LicenseNumber { get; set; } = string.Empty;

    public string? LicenseCategory { get; set; }

    public DateTime LicenseExpiryDateUtc { get; set; }

    /// <summary>The vehicle the driver usually uses (must belong to the same branch).</summary>
    public long? DefaultVehicleId { get; set; }

    public MasterData.Vehicle? DefaultVehicle { get; set; }

    public string? Notes { get; set; }
}
