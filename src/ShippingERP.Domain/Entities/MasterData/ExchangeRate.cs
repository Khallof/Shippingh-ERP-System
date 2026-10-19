using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.MasterData;

/// <summary>
/// 1 FromCurrency = Rate x ToCurrency, valid from EffectiveFromUtc until a newer rate for the same pair.
/// Rates are kept as history: add a new row instead of editing an old one that was already used.
/// </summary>
public class ExchangeRate : SoftDeletableEntity
{
    public long FromCurrencyId { get; set; }

    public Currency FromCurrency { get; set; } = null!;

    public long ToCurrencyId { get; set; }

    public Currency ToCurrency { get; set; } = null!;

    /// <summary>decimal(18,6).</summary>
    public decimal Rate { get; set; }

    public DateTime EffectiveFromUtc { get; set; }

    public string? Notes { get; set; }
}
