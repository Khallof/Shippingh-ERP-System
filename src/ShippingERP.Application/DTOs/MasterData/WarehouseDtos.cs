using ShippingERP.Application.DTOs.Common;

namespace ShippingERP.Application.DTOs.MasterData;

public sealed class WarehouseDto : MasterDataDto
{
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public decimal? CapacityCubicMeters { get; init; }
    public string? Notes { get; init; }
}

public sealed class SaveWarehouseRequest
{
    public string Code { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
    public long BranchId { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public decimal? CapacityCubicMeters { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Search: code, names. SortBy: code (default), arabicName, branch, createdAtUtc.</summary>
public sealed class WarehouseListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "code", "arabicName", "branch", "createdAtUtc" };

    public long? BranchId { get; set; }
}
