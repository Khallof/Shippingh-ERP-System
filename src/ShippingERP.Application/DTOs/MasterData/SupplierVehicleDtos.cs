using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.MasterData;

// ---------------- Supplier ----------------

public sealed class SupplierDto : MasterDataDto
{
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public string? ContactPerson { get; init; }
    public string Phone { get; init; } = string.Empty;
    public string? AlternatePhone { get; init; }
    public string? Email { get; init; }
    public string? TaxNumber { get; init; }
    public string? Address { get; init; }
    public decimal OpeningBalance { get; init; }
    public string? Notes { get; init; }
}

public sealed class SaveSupplierRequest
{
    public string Code { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
    public string? ContactPerson { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? AlternatePhone { get; set; }
    public string? Email { get; set; }
    public string? TaxNumber { get; set; }
    public string? Address { get; set; }
    public decimal OpeningBalance { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Search: code, names, phones, contact person. SortBy: code (default), arabicName, createdAtUtc.</summary>
public sealed class SupplierListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "code", "arabicName", "createdAtUtc" };
}

// ---------------- Vehicle ----------------

public sealed class VehicleDto : MasterDataDto
{
    public string PlateNumber { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public VehicleType VehicleType { get; init; }
    public decimal CapacityKg { get; init; }
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

public sealed class SaveVehicleRequest
{
    /// <summary>Stored upper case with single spaces, unique.</summary>
    public string PlateNumber { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    /// <summary>Motorcycle, Car, Van, Truck, Other.</summary>
    public VehicleType VehicleType { get; set; }
    public decimal CapacityKg { get; set; }
    public long BranchId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Search: plate number, model. SortBy: plateNumber (default), model, capacityKg, createdAtUtc.</summary>
public sealed class VehicleListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "plateNumber", "model", "capacityKg", "createdAtUtc" };

    public long? BranchId { get; set; }

    public VehicleType? VehicleType { get; set; }
}
