using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.MasterData;

/// <summary>
/// A currency. Code = ISO 4217, e.g. "SYP", "USD". Exactly one currency is the base currency
/// (used by accounting); all amounts in other currencies are converted with ExchangeRate.
/// </summary>
public class Currency : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    public string Symbol { get; set; } = string.Empty;

    /// <summary>Number of decimals allowed in amounts (0 to 4).</summary>
    public int DecimalPlaces { get; set; } = 2;

    public bool IsBaseCurrency { get; set; }
}
