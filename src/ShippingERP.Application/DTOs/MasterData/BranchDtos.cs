using ShippingERP.Application.DTOs.Common;

namespace ShippingERP.Application.DTOs.MasterData;

public sealed class BranchDto : MasterDataDto
{
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public long CityId { get; init; }
    public string CityName { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public bool IsHeadOffice { get; init; }
    public string? Notes { get; init; }
    public int WarehouseCount { get; init; }
}

/// <summary>Body of POST /api/v1/branches and PUT /api/v1/branches/{id}.</summary>
public sealed class SaveBranchRequest
{
    public string Code { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
    public long CityId { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsHeadOffice { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Search: code, names, phone. SortBy: code (default), arabicName, englishName, createdAtUtc.</summary>
public sealed class BranchListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "code", "arabicName", "englishName", "createdAtUtc" };

    public long? CityId { get; set; }
}
