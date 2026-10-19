using ShippingERP.Application.DTOs.Common;

namespace ShippingERP.Application.DTOs.MasterData;

// ---------------- Currency ----------------

public sealed class CurrencyDto : MasterDataDto
{
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public int DecimalPlaces { get; init; }
    public bool IsBaseCurrency { get; init; }
}

public sealed class SaveCurrencyRequest
{
    /// <summary>ISO 4217, 3 letters, e.g. "SYP".</summary>
    public string Code { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
    public string Symbol { get; set; } = string.Empty;
    /// <summary>0 to 4.</summary>
    public int DecimalPlaces { get; set; } = 2;
    /// <summary>Setting true makes this the only base currency.</summary>
    public bool IsBaseCurrency { get; set; }
}

/// <summary>Search: code, names. SortBy: code (default), arabicName.</summary>
public sealed class CurrencyListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "code", "arabicName" };
}

// ---------------- Exchange rate ----------------

public sealed class ExchangeRateDto : MasterDataDto
{
    public long FromCurrencyId { get; init; }
    public string FromCurrencyCode { get; init; } = string.Empty;
    public long ToCurrencyId { get; init; }
    public string ToCurrencyCode { get; init; } = string.Empty;
    /// <summary>1 From = Rate x To.</summary>
    public decimal Rate { get; init; }
    public DateTime EffectiveFromUtc { get; init; }
    public string? Notes { get; init; }
}

public sealed class SaveExchangeRateRequest
{
    public long FromCurrencyId { get; set; }
    public long ToCurrencyId { get; set; }
    /// <summary>Greater than 0, at most 6 decimals.</summary>
    public decimal Rate { get; set; }
    /// <summary>UTC date-time from which the rate applies, e.g. "2026-10-11T00:00:00Z".</summary>
    public DateTime EffectiveFromUtc { get; set; }
    public string? Notes { get; set; }
}

/// <summary>SortBy: effectiveFromUtc (default, newest first when desc), rate.</summary>
public sealed class ExchangeRateListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "effectiveFromUtc", "rate" };

    public long? FromCurrencyId { get; set; }

    public long? ToCurrencyId { get; set; }
}
