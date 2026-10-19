using ShippingERP.Domain.Entities.Pricing;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Pricing;

/// <summary>The shipment facts a price rule is matched against.</summary>
public sealed class PricingContext
{
    public long? OriginZoneId { get; init; }
    public long? DestinationZoneId { get; init; }
    public long? OriginCityId { get; init; }
    public long? DestinationCityId { get; init; }
    public long ServiceTypeId { get; init; }
    public decimal ChargeableWeight { get; init; }
    public int PackageCount { get; init; }

    /// <summary>Step 9: rules with AppliesToBasis only match shipments priced the same way.</summary>
    public PricingBasis PricingBasis { get; init; } = PricingBasis.Weight;
}

/// <summary>
/// Chooses the price rule of a plan for a shipment. Pure logic (no database), so it is unit-tested directly.
///
/// A rule matches when it is active, valid at the pricing time and, for every condition TYPE it has, at least one
/// condition of that type matches (same type = OR, different types = AND). Winner: highest Priority, then the most
/// specific rule (more condition types), then the lowest Id (the oldest rule).
/// </summary>
public static class PriceRuleMatcher
{
    public static bool Matches(PriceRule rule, PricingContext context, DateTime nowUtc)
    {
        if (!rule.IsActive || rule.IsDeleted)
        {
            return false;
        }

        if ((rule.ValidFromUtc.HasValue && rule.ValidFromUtc.Value > nowUtc) ||
            (rule.ValidToUtc.HasValue && rule.ValidToUtc.Value <= nowUtc))
        {
            return false;
        }

        if (rule.AppliesToBasis.HasValue && rule.AppliesToBasis.Value != context.PricingBasis)
        {
            return false;
        }

        foreach (var group in rule.Conditions.GroupBy(x => x.ConditionType))
        {
            if (!group.Any(condition => ConditionMatches(condition, context)))
            {
                return false;
            }
        }

        return true;
    }

    public static bool ConditionMatches(PriceRuleCondition condition, PricingContext context)
    {
        return condition.ConditionType switch
        {
            PriceConditionType.OriginZone => context.OriginZoneId.HasValue && condition.ReferenceId == context.OriginZoneId,
            PriceConditionType.DestinationZone => context.DestinationZoneId.HasValue && condition.ReferenceId == context.DestinationZoneId,
            PriceConditionType.OriginCity => context.OriginCityId.HasValue && condition.ReferenceId == context.OriginCityId,
            PriceConditionType.DestinationCity => context.DestinationCityId.HasValue && condition.ReferenceId == context.DestinationCityId,
            PriceConditionType.ServiceType => condition.ReferenceId == context.ServiceTypeId,
            PriceConditionType.ChargeableWeight => InRange(context.ChargeableWeight, condition),
            PriceConditionType.PackageCount => InRange(context.PackageCount, condition),
            _ => false
        };
    }

    /// <summary>Number of different condition types (more = more specific).</summary>
    public static int Specificity(PriceRule rule) =>
        rule.Conditions.Select(x => x.ConditionType).Distinct().Count() + (rule.AppliesToBasis.HasValue ? 1 : 0);

    public static PriceRule? SelectBest(IEnumerable<PriceRule> rules, PricingContext context, DateTime nowUtc)
    {
        return rules
            .Where(rule => Matches(rule, context, nowUtc))
            .OrderByDescending(rule => rule.Priority)
            .ThenByDescending(Specificity)
            .ThenBy(rule => rule.Id)
            .FirstOrDefault();
    }

    private static bool InRange(decimal value, PriceRuleCondition condition) =>
        (condition.MinValue is null || value >= condition.MinValue.Value) &&
        (condition.MaxValue is null || value < condition.MaxValue.Value);
}

/// <summary>Everything besides the rule that the price depends on.</summary>
public sealed class PriceBreakdownInput
{
    public decimal ChargeableWeight { get; init; }

    /// <summary>Step 9: Weight = base + weight tiers; Volume = base + VolumeCbm x PricePerCubicMeter (no weight tiers).</summary>
    public PricingBasis PricingBasis { get; init; } = PricingBasis.Weight;

    /// <summary>Volume in m³ (3 decimals), charged when PricingBasis = Volume.</summary>
    public decimal VolumeCbm { get; init; }

    public int PackageCount { get; init; }
    public bool IsCod { get; init; }
    public decimal CodAmount { get; init; }
    public decimal InsuranceValue { get; init; }
    public bool RequiresPickup { get; init; }
    public bool RequiresPackaging { get; init; }
    public bool IsRemoteArea { get; init; }
    public bool IsReturn { get; init; }

    /// <summary>Manual shipping subtotal (replaces base + weight + service fees and the contract discount).</summary>
    public decimal? ManualShippingCharge { get; init; }

    public decimal ManualDiscountAmount { get; init; }
}

/// <summary>One applied weight tier.</summary>
public sealed record WeightTierCharge(decimal FromKg, decimal? ToKg, decimal Kg, decimal PricePerKg, decimal Charge);

/// <summary>The full price breakdown (all amounts rounded to the currency decimals).</summary>
public sealed class PriceBreakdown
{
    public decimal BasePrice { get; init; }
    public decimal IncludedWeightKg { get; init; }
    public decimal AdditionalWeightKg { get; init; }
    public decimal WeightCharge { get; init; }
    public IReadOnlyList<WeightTierCharge> WeightTiers { get; init; } = Array.Empty<WeightTierCharge>();

    public PricingBasis PricingBasis { get; init; } = PricingBasis.Weight;
    public decimal VolumeCbm { get; init; }
    public decimal PricePerCubicMeter { get; init; }

    /// <summary>VolumeCbm x PricePerCubicMeter (0 for weight pricing).</summary>
    public decimal VolumeCharge { get; init; }

    public decimal PackageFee { get; init; }
    public decimal InsuranceFee { get; init; }
    public decimal PickupFee { get; init; }
    public decimal PackagingFee { get; init; }
    public decimal RemoteAreaFee { get; init; }
    public decimal ReturnFee { get; init; }
    public decimal ServiceFeesTotal => PackageFee + InsuranceFee + PickupFee + PackagingFee + RemoteAreaFee + ReturnFee;

    public decimal CodFeeFixed { get; init; }
    public decimal CodFeePercentage { get; init; }
    public decimal CodFeeMinimum { get; init; }
    public decimal CodAmount { get; init; }
    public decimal CodCommission { get; init; }

    /// <summary>Base + weight (or volume) + service fees, or the manual charge.</summary>
    public decimal ShippingSubtotal { get; init; }

    /// <summary>The rule subtotal when a manual charge replaced it.</summary>
    public decimal? CalculatedShippingSubtotal { get; init; }

    public decimal ContractDiscountPercentage { get; init; }
    public decimal ContractDiscountAmount { get; init; }
    public decimal ManualDiscountAmount { get; init; }
    public decimal DiscountAmount => ContractDiscountAmount + ManualDiscountAmount;

    /// <summary>ShippingSubtotal - DiscountAmount.</summary>
    public decimal TotalShippingCharge => ShippingSubtotal - DiscountAmount;

    /// <summary>TotalShippingCharge + COD commission.</summary>
    public decimal TaxableAmount => TotalShippingCharge + CodCommission;

    public decimal TaxPercentage { get; init; }
    public decimal TaxAmount { get; init; }

    /// <summary>Net total = TaxableAmount + TaxAmount.</summary>
    public decimal NetTotal => TaxableAmount + TaxAmount;

    /// <summary>Gross charge stored on the shipment: ShippingSubtotal + COD commission.</summary>
    public decimal GrossCharge => ShippingSubtotal + CodCommission;
}

/// <summary>
/// Calculates the price breakdown of a shipment from one rule (pure logic, see PriceRule for the formulas).
///
/// - With a manual charge, base / weight / service fees are not used (the manual charge is the shipping subtotal)
///   and no contract discount applies; the COD commission still comes from the rule when there is one.
/// - The tax applies to (subtotal - discount + COD commission).
/// - Every component is rounded to the currency decimals (midpoint away from zero) before it is added, so the
///   printed components always add up to the totals.
/// </summary>
public static class PriceBreakdownCalculator
{
    public static PriceBreakdown Calculate(PriceRule? rule, PriceBreakdownInput input, decimal taxPercentage, int decimals)
    {
        if (rule is null && input.ManualShippingCharge is null)
        {
            throw new InvalidOperationException("A price needs a rule or a manual shipping charge.");
        }

        decimals = Math.Clamp(decimals, 0, 4);

        // ---------------- COD commission (from the rule, also with a manual charge)
        decimal codCommission = 0m;
        if (rule is not null && input.IsCod && input.CodAmount > 0m)
        {
            var commission = rule.CodFeeFixed + (input.CodAmount * rule.CodFeePercentage / 100m);
            codCommission = Round(Math.Max(commission, rule.CodFeeMinimum), decimals);
        }

        decimal basePrice = 0m, weightCharge = 0m, additionalKg = 0m, volumeCharge = 0m, pricePerCbm = 0m;
        var volume = input.PricingBasis == PricingBasis.Volume ? Math.Max(0m, input.VolumeCbm) : 0m;
        decimal packageFee = 0m, insuranceFee = 0m, pickupFee = 0m, packagingFee = 0m, remoteAreaFee = 0m, returnFee = 0m;
        decimal contractDiscount = 0m, contractDiscountPercentage = 0m;
        IReadOnlyList<WeightTierCharge> tiers = Array.Empty<WeightTierCharge>();
        decimal subtotal;
        decimal? calculatedSubtotal = null;

        if (rule is not null)
        {
            basePrice = Round(rule.BasePrice, decimals);
            if (input.PricingBasis == PricingBasis.Volume)
            {
                pricePerCbm = rule.PricePerCubicMeter;
                volumeCharge = Round(volume * pricePerCbm, decimals);
            }
            else
            {
                (tiers, additionalKg) = WeightTiers(rule, input.ChargeableWeight, decimals);
                weightCharge = tiers.Sum(x => x.Charge);
            }

            var extraPackages = Math.Max(0, input.PackageCount - Math.Max(0, rule.IncludedPackageCount));
            packageFee = Round(extraPackages * rule.PerPackageFee, decimals);

            if (input.InsuranceValue > 0m && (rule.InsuranceFeePercentage > 0m || rule.InsuranceFeeMinimum > 0m))
            {
                var fee = input.InsuranceValue * rule.InsuranceFeePercentage / 100m;
                insuranceFee = Round(Math.Max(fee, rule.InsuranceFeeMinimum), decimals);
            }

            pickupFee = input.RequiresPickup ? Round(rule.PickupFee, decimals) : 0m;
            packagingFee = input.RequiresPackaging ? Round(rule.PackagingFee, decimals) : 0m;
            remoteAreaFee = input.IsRemoteArea ? Round(rule.RemoteAreaFee, decimals) : 0m;
            returnFee = input.IsReturn ? Round(rule.ReturnFee, decimals) : 0m;

            calculatedSubtotal = basePrice + weightCharge + volumeCharge + packageFee + insuranceFee + pickupFee + packagingFee + remoteAreaFee + returnFee;
        }

        if (input.ManualShippingCharge.HasValue)
        {
            subtotal = Round(input.ManualShippingCharge.Value, decimals);
            basePrice = weightCharge = additionalKg = volumeCharge = pricePerCbm = 0m;
            packageFee = insuranceFee = pickupFee = packagingFee = remoteAreaFee = returnFee = 0m;
            tiers = Array.Empty<WeightTierCharge>();
        }
        else
        {
            subtotal = calculatedSubtotal!.Value;
            calculatedSubtotal = null;
            contractDiscountPercentage = rule!.DiscountPercentage;
            contractDiscount = Round(subtotal * contractDiscountPercentage / 100m, decimals);
        }

        var manualDiscount = Round(input.ManualDiscountAmount, decimals);
        var taxable = subtotal - contractDiscount - manualDiscount + codCommission;
        var tax = Round(Math.Max(0m, taxable) * taxPercentage / 100m, decimals);

        return new PriceBreakdown
        {
            BasePrice = basePrice,
            IncludedWeightKg = input.ManualShippingCharge.HasValue || input.PricingBasis == PricingBasis.Volume ? 0m : rule?.IncludedWeightKg ?? 0m,
            AdditionalWeightKg = additionalKg,
            WeightCharge = weightCharge,
            WeightTiers = tiers,
            PricingBasis = input.PricingBasis,
            VolumeCbm = volume,
            PricePerCubicMeter = pricePerCbm,
            VolumeCharge = volumeCharge,
            PackageFee = packageFee,
            InsuranceFee = insuranceFee,
            PickupFee = pickupFee,
            PackagingFee = packagingFee,
            RemoteAreaFee = remoteAreaFee,
            ReturnFee = returnFee,
            CodFeeFixed = rule?.CodFeeFixed ?? 0m,
            CodFeePercentage = rule?.CodFeePercentage ?? 0m,
            CodFeeMinimum = rule?.CodFeeMinimum ?? 0m,
            CodAmount = input.IsCod ? input.CodAmount : 0m,
            CodCommission = codCommission,
            ShippingSubtotal = subtotal,
            CalculatedShippingSubtotal = calculatedSubtotal,
            ContractDiscountPercentage = contractDiscountPercentage,
            ContractDiscountAmount = contractDiscount,
            ManualDiscountAmount = manualDiscount,
            TaxPercentage = taxPercentage,
            TaxAmount = tax
        };
    }

    /// <summary>Progressive tiers: each tier charges the kg inside [from, to) above the included weight.</summary>
    public static (IReadOnlyList<WeightTierCharge> Tiers, decimal Kg) WeightTiers(PriceRule rule, decimal chargeableWeight, int decimals)
    {
        var result = new List<WeightTierCharge>();
        var included = Math.Max(0m, rule.IncludedWeightKg);
        decimal totalKg = 0m;

        foreach (var tier in rule.WeightTiers.OrderBy(x => x.FromWeightKg))
        {
            var start = Math.Max(tier.FromWeightKg, included);
            var end = Math.Min(chargeableWeight, tier.ToWeightKg ?? decimal.MaxValue);
            var kg = end - start;
            if (kg <= 0m)
            {
                continue;
            }

            var charge = Round(kg * tier.PricePerKg, decimals);
            result.Add(new WeightTierCharge(tier.FromWeightKg, tier.ToWeightKg, kg, tier.PricePerKg, charge));
            totalKg += kg;
        }

        return (result, totalKg);
    }

    public static decimal Round(decimal value, int decimals) =>
        Math.Round(value, Math.Clamp(decimals, 0, 4), MidpointRounding.AwayFromZero);
}
