using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.Pricing;

/// <summary>
/// A weight tier of a price rule (شريحة وزن). Tiers are progressive, like tax brackets: each tier charges only the
/// kilograms that fall inside [FromWeightKg, ToWeightKg) AND above the rule's IncludedWeightKg.
///
/// Example (IncludedWeightKg = 5): [5, 20) at 2,000 / kg and [20, open) at 1,500 / kg.
/// A 25 kg shipment pays 15 x 2,000 + 5 x 1,500 on top of the base price.
///
/// Rules: tiers do not overlap, follow each other without gaps, and only the last one may be open-ended.
/// </summary>
public class PriceRuleWeightTier : BaseEntity
{
    public long PriceRuleId { get; set; }

    public PriceRule PriceRule { get; set; } = null!;

    /// <summary>Inclusive, decimal(10,3).</summary>
    public decimal FromWeightKg { get; set; }

    /// <summary>Exclusive, decimal(10,3). Null = no upper limit (last tier only).</summary>
    public decimal? ToWeightKg { get; set; }

    public decimal PricePerKg { get; set; }
}
