using ShippingERP.Application.DTOs.Common;

namespace ShippingERP.Application.DTOs.HR;

public sealed class EmployeeDto : MasterDataDto
{
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public string? JobTitle { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? NationalId { get; init; }
    public DateTime? HireDateUtc { get; init; }
    public long? UserId { get; init; }
    public string? UserName { get; init; }
    public bool IsDriver { get; init; }
    public string? Notes { get; init; }
}

public sealed class SaveEmployeeRequest
{
    public string Code { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
    public long BranchId { get; set; }
    public string? JobTitle { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? NationalId { get; set; }
    public DateTime? HireDateUtc { get; set; }
    /// <summary>Optional system user (one user per employee).</summary>
    public long? UserId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Search: code, names, phone, job title. SortBy: code (default), arabicName, hireDateUtc, createdAtUtc.</summary>
public sealed class EmployeeListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "code", "arabicName", "hireDateUtc", "createdAtUtc" };

    public long? BranchId { get; set; }

    public bool? HasUser { get; set; }
}

// ---------------- Driver ----------------

public sealed class DriverDto : MasterDataDto
{
    public long EmployeeId { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string EmployeeName { get; init; } = string.Empty;
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public long? UserId { get; init; }
    public string? UserName { get; init; }
    public string LicenseNumber { get; init; } = string.Empty;
    public string? LicenseCategory { get; init; }
    public DateTime LicenseExpiryDateUtc { get; init; }
    public bool IsLicenseExpired { get; init; }
    public long? DefaultVehicleId { get; init; }
    public string? DefaultVehiclePlate { get; init; }
    public string? Notes { get; init; }
}

public sealed class SaveDriverRequest
{
    public long EmployeeId { get; set; }
    /// <summary>Optional; when empty, the employee's user (if any) is used.</summary>
    public long? UserId { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string? LicenseCategory { get; set; }
    public DateTime LicenseExpiryDateUtc { get; set; }
    /// <summary>Must belong to the employee's branch.</summary>
    public long? DefaultVehicleId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Search: employee code/name, license number, phone. SortBy: employeeName (default), licenseExpiryDateUtc.</summary>
public sealed class DriverListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "employeeName", "licenseExpiryDateUtc" };

    public long? BranchId { get; set; }

    /// <summary>true = only drivers whose license expires within 30 days or already expired.</summary>
    public bool? LicenseExpiringSoon { get; set; }
}
