using ShippingERP.Application.DTOs.Common;

namespace ShippingERP.Application.DTOs.MasterData;

// ---------------- Country ----------------

public sealed class CountryDto : MasterDataDto
{
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public string? PhoneCode { get; init; }
    public int CityCount { get; init; }
}

public sealed class SaveCountryRequest
{
    /// <summary>ISO code, 2 or 3 letters, e.g. "SY".</summary>
    public string Code { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
    /// <summary>e.g. "+963".</summary>
    public string? PhoneCode { get; set; }
}

/// <summary>Search: code, names. SortBy: code, arabicName (default), englishName.</summary>
public sealed class CountryListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "code", "arabicName", "englishName" };
}

// ---------------- City ----------------

public sealed class CityDto : MasterDataDto
{
    public long CountryId { get; init; }
    public string CountryName { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public int ZoneCount { get; init; }
}

public sealed class SaveCityRequest
{
    public long CountryId { get; set; }
    /// <summary>Unique inside the country, e.g. "DAM".</summary>
    public string Code { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
}

/// <summary>Search: code, names. SortBy: code, arabicName (default), englishName.</summary>
public sealed class CityListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "code", "arabicName", "englishName" };

    public long? CountryId { get; set; }
}

// ---------------- Zone ----------------

public sealed class ZoneDto : MasterDataDto
{
    public long CityId { get; init; }
    public string CityName { get; init; } = string.Empty;
    public long CountryId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    /// <summary>Shipments to this zone pay the remote area fee of the price rule (step 5).</summary>
    public bool IsRemoteArea { get; init; }
    public string? Notes { get; init; }
}

public sealed class SaveZoneRequest
{
    public long CityId { get; set; }
    /// <summary>Unique inside the city, e.g. "MAZZEH".</summary>
    public string Code { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
    /// <summary>Hard-to-reach area (remote area fee).</summary>
    public bool IsRemoteArea { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Search: code, names. SortBy: code, arabicName (default), englishName.</summary>
public sealed class ZoneListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "code", "arabicName", "englishName" };

    public long? CityId { get; set; }

    public long? CountryId { get; set; }

    public bool? IsRemoteArea { get; set; }
}
