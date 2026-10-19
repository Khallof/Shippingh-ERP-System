using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Pricing;

/// <summary>
/// One price rule of a pricing plan (قاعدة تسعير). Replaces the step-3 CustomerPricingPlanRule.
///
/// WHEN it applies: its <see cref="Conditions"/> (zones, cities, service type, weight range, package count).
/// When several active rules match a shipment, the highest Priority wins, then the most specific rule
/// (the one with more condition types), then the oldest rule (lowest Id).
///
/// WHAT it charges (all amounts in the plan currency):
///   Base charge      = BasePrice (covers the first IncludedWeightKg and IncludedPackageCount)
///   Weight charge    = sum of the weight tiers above IncludedWeightKg (progressive, like tax brackets)
///   Package fee      = (packages - IncludedPackageCount) x PerPackageFee
///   Insurance fee    = max(InsuranceFeeMinimum, insurance value x InsuranceFeePercentage %)   (insured shipments)
///   Pickup fee       = PickupFee        (shipments with RequiresPickup)
///   Packaging fee    = PackagingFee     (shipments with RequiresPackaging)
///   Remote area fee  = RemoteAreaFee    (destination zone marked IsRemoteArea)
///   Return fee       = ReturnFee        (shipments being returned to the sender)
///   Contract discount= DiscountPercentage % of the shipping subtotal
///   COD commission   = max(CodFeeMinimum, CodFeeFixed + COD amount x CodFeePercentage %)   (COD shipments)
///   Tax              = TaxRate (or the default tax rate when empty) on (subtotal - discount + COD commission)
///
/// Shipments keep the values used at pricing time in ShipmentPricingSnapshot, so editing a rule never changes the
/// price of an existing shipment.
/// </summary>
public class PriceRule : SoftDeletableEntity
{
    public long PricingPlanId { get; set; }

    public CustomerPricingPlan PricingPlan { get; set; } = null!;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    /// <summary>0 to 1000. Higher wins.</summary>
    public int Priority { get; set; }

    /// <summary>Optional validity inside the plan validity (e.g. a seasonal surcharge).</summary>
    public DateTime? ValidFromUtc { get; set; }

    public DateTime? ValidToUtc { get; set; }

    // ---------------- Base and weight ----------------

    public decimal BasePrice { get; set; }

    /// <summary>Weight covered by the base price (kg, 3 decimals). Tiers charge the weight above it.</summary>
    public decimal IncludedWeightKg { get; set; }

    // ---------------- Volume (step 9) ----------------

    /// <summary>Price of one cubic metre; used for shipments priced by volume (charge = m³ x price, plus the base price).</summary>
    public decimal PricePerCubicMeter { get; set; }

    /// <summary>Null = the rule prices both kinds; otherwise only shipments priced by weight, or only by volume.</summary>
    public PricingBasis? AppliesToBasis { get; set; }

    // ---------------- Packages ----------------

    /// <summary>Packages covered by the base price (default 1).</summary>
    public int IncludedPackageCount { get; set; } = 1;

    public decimal PerPackageFee { get; set; }

    // ---------------- COD commission ----------------

    public decimal CodFeeFixed { get; set; }

    /// <summary>0 to 100.</summary>
    public decimal CodFeePercentage { get; set; }

    public decimal CodFeeMinimum { get; set; }

    // ---------------- Insurance ----------------

    /// <summary>0 to 100, of Shipment.InsuranceValue.</summary>
    public decimal InsuranceFeePercentage { get; set; }

    public decimal InsuranceFeeMinimum { get; set; }

    // ---------------- Optional service fees ----------------

    public decimal PickupFee { get; set; }

    public decimal PackagingFee { get; set; }

    public decimal RemoteAreaFee { get; set; }

    public decimal ReturnFee { get; set; }

    // ---------------- Discount and tax ----------------

    /// <summary>Contract discount 0 to 100 % on the shipping subtotal (base + weight + service fees).</summary>
    public decimal DiscountPercentage { get; set; }

    /// <summary>Tax rate of this rule. Null = the default tax rate.</summary>
    public long? TaxRateId { get; set; }

    public TaxRate? TaxRate { get; set; }

    public string? Notes { get; set; }

    public ICollection<PriceRuleCondition> Conditions { get; set; } = new List<PriceRuleCondition>();

    public ICollection<PriceRuleWeightTier> WeightTiers { get; set; } = new List<PriceRuleWeightTier>();
}
