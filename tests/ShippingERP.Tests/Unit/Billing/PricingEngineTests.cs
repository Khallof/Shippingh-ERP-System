using ShippingERP.Domain.Entities.Pricing;
using ShippingERP.Domain.Enums;
using ShippingERP.Domain.Pricing;

namespace ShippingERP.Tests.Unit.Billing;

/// <summary>The pure pricing engine: rule matching and the price breakdown (no database).</summary>
public sealed class PricingEngineTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);

    private static PriceRule Rule(long id, int priority = 10, params PriceRuleCondition[] conditions)
    {
        var rule = new PriceRule { Id = id, ArabicName = $"R{id}", Priority = priority, IsActive = true, IncludedPackageCount = 1 };
        foreach (var condition in conditions)
        {
            rule.Conditions.Add(condition);
        }

        return rule;
    }

    private static PriceRuleCondition Ref(PriceConditionType type, long id) => new() { ConditionType = type, ReferenceId = id };

    private static PriceRuleCondition Range(PriceConditionType type, decimal? min, decimal? max) => new() { ConditionType = type, MinValue = min, MaxValue = max };

    private static PricingContext Context(long serviceType = 1, long? destinationZone = 100, decimal weight = 3m, int packages = 1) => new()
    {
        ServiceTypeId = serviceType,
        DestinationZoneId = destinationZone,
        OriginZoneId = 200,
        OriginCityId = 10,
        DestinationCityId = 20,
        ChargeableWeight = weight,
        PackageCount = packages
    };

    // ------------------------------------------------------------------ matching

    [Fact]
    public void Conditions_OfTheSameType_AreAlternatives_DifferentTypes_MustAllMatch()
    {
        var rule = Rule(1, 10,
            Ref(PriceConditionType.DestinationZone, 100),
            Ref(PriceConditionType.DestinationZone, 101),
            Ref(PriceConditionType.ServiceType, 1));

        Assert.True(PriceRuleMatcher.Matches(rule, Context(destinationZone: 100), Now));
        Assert.True(PriceRuleMatcher.Matches(rule, Context(destinationZone: 101), Now));
        Assert.False(PriceRuleMatcher.Matches(rule, Context(destinationZone: 102), Now));
        Assert.False(PriceRuleMatcher.Matches(rule, Context(serviceType: 2, destinationZone: 100), Now));

        // A zone condition never matches a shipment without a zone.
        Assert.False(PriceRuleMatcher.Matches(rule, Context(destinationZone: null), Now));
    }

    [Fact]
    public void RangeConditions_AreInclusiveMinAndExclusiveMax()
    {
        var rule = Rule(1, 10, Range(PriceConditionType.ChargeableWeight, 5m, 10m), Range(PriceConditionType.PackageCount, 2m, null));

        Assert.True(PriceRuleMatcher.Matches(rule, Context(weight: 5m, packages: 2), Now));
        Assert.True(PriceRuleMatcher.Matches(rule, Context(weight: 9.999m, packages: 50), Now));
        Assert.False(PriceRuleMatcher.Matches(rule, Context(weight: 10m, packages: 2), Now));
        Assert.False(PriceRuleMatcher.Matches(rule, Context(weight: 6m, packages: 1), Now));
    }

    [Fact]
    public void SelectBest_PrefersPriority_ThenSpecificity_ThenTheOldestRule()
    {
        var general = Rule(1, 10);
        var specific = Rule(2, 10, Ref(PriceConditionType.ServiceType, 1), Ref(PriceConditionType.DestinationZone, 100));
        var otherSpecific = Rule(3, 10, Ref(PriceConditionType.ServiceType, 1), Ref(PriceConditionType.OriginZone, 200));
        var promotion = Rule(4, 50, Range(PriceConditionType.ChargeableWeight, 0m, 2m));

        var rules = new[] { general, specific, otherSpecific, promotion };

        // Weight 3: the promotion does not match; two rules with 2 condition types: the older one (Id 2) wins.
        Assert.Same(specific, PriceRuleMatcher.SelectBest(rules, Context(weight: 3m), Now));

        // Weight 1: the higher priority wins although it is less specific.
        Assert.Same(promotion, PriceRuleMatcher.SelectBest(rules, Context(weight: 1m), Now));
    }

    [Fact]
    public void InactiveRules_AndRulesOutsideTheirValidity_NeverMatch()
    {
        var inactive = Rule(1);
        inactive.IsActive = false;
        var future = Rule(2);
        future.ValidFromUtc = Now.AddDays(1);
        var expired = Rule(3);
        expired.ValidToUtc = Now;

        Assert.Null(PriceRuleMatcher.SelectBest(new[] { inactive, future, expired }, Context(), Now));
    }

    // ------------------------------------------------------------------ breakdown

    private static PriceRule DemoStandard()
    {
        var rule = Rule(1);
        rule.BasePrice = 15000m;
        rule.IncludedWeightKg = 5m;
        rule.WeightTiers.Add(new PriceRuleWeightTier { FromWeightKg = 5m, ToWeightKg = 20m, PricePerKg = 2000m });
        rule.WeightTiers.Add(new PriceRuleWeightTier { FromWeightKg = 20m, ToWeightKg = null, PricePerKg = 1500m });
        rule.CodFeeFixed = 2000m;
        rule.CodFeePercentage = 1m;
        return rule;
    }

    [Theory]
    [InlineData(2.5, 15000, 0)]
    [InlineData(5, 15000, 0)]
    [InlineData(9, 23000, 8000)]
    [InlineData(25, 52500, 37500)] // 15 x 2,000 + 5 x 1,500
    public void WeightTiers_AreProgressive_AboveTheIncludedWeight(decimal weight, decimal subtotal, decimal weightCharge)
    {
        var breakdown = PriceBreakdownCalculator.Calculate(DemoStandard(), new PriceBreakdownInput { ChargeableWeight = weight, PackageCount = 1 }, 0m, 0);

        Assert.Equal(15000m, breakdown.BasePrice);
        Assert.Equal(weightCharge, breakdown.WeightCharge);
        Assert.Equal(subtotal, breakdown.ShippingSubtotal);
        Assert.Equal(subtotal, breakdown.NetTotal);
    }

    [Fact]
    public void ServiceFees_DiscountCodCommissionAndTax_FollowTheDocumentedFormula()
    {
        var rule = DemoStandard();
        rule.IncludedPackageCount = 1;
        rule.PerPackageFee = 1000m;
        rule.InsuranceFeePercentage = 1m;
        rule.InsuranceFeeMinimum = 1000m;
        rule.PickupFee = 3000m;
        rule.PackagingFee = 2500m;
        rule.RemoteAreaFee = 5000m;
        rule.ReturnFee = 7500m;
        rule.DiscountPercentage = 10m;

        var breakdown = PriceBreakdownCalculator.Calculate(rule, new PriceBreakdownInput
        {
            ChargeableWeight = 6m,
            PackageCount = 3,
            InsuranceValue = 300000m,
            RequiresPickup = true,
            RequiresPackaging = true,
            IsRemoteArea = true,
            IsReturn = false,
            IsCod = true,
            CodAmount = 100000m
        }, taxPercentage: 5m, decimals: 0);

        Assert.Equal(15000m, breakdown.BasePrice);
        Assert.Equal(2000m, breakdown.WeightCharge);          // 1 kg above 5
        Assert.Equal(2000m, breakdown.PackageFee);            // 2 extra packages
        Assert.Equal(3000m, breakdown.InsuranceFee);          // 1 % of 300,000
        Assert.Equal(3000m, breakdown.PickupFee);
        Assert.Equal(2500m, breakdown.PackagingFee);
        Assert.Equal(5000m, breakdown.RemoteAreaFee);
        Assert.Equal(0m, breakdown.ReturnFee);
        Assert.Equal(15500m, breakdown.ServiceFeesTotal);
        Assert.Equal(32500m, breakdown.ShippingSubtotal);
        Assert.Equal(3250m, breakdown.ContractDiscountAmount); // 10 %
        Assert.Equal(29250m, breakdown.TotalShippingCharge);
        Assert.Equal(3000m, breakdown.CodCommission);         // 2,000 + 1 % x 100,000
        Assert.Equal(32250m, breakdown.TaxableAmount);
        Assert.Equal(1613m, breakdown.TaxAmount);             // 5 % = 1,612.5 -> 1,613 (SYP, 0 decimals, away from zero)
        Assert.Equal(33863m, breakdown.NetTotal);
        Assert.Equal(35500m, breakdown.GrossCharge);          // subtotal + COD commission
    }

    [Fact]
    public void Minimums_ApplyToSmallInsuranceAndCodAmounts()
    {
        var rule = DemoStandard();
        rule.InsuranceFeePercentage = 1m;
        rule.InsuranceFeeMinimum = 1000m;
        rule.CodFeeFixed = 0m;
        rule.CodFeePercentage = 1m;
        rule.CodFeeMinimum = 2500m;

        var breakdown = PriceBreakdownCalculator.Calculate(rule, new PriceBreakdownInput
        {
            ChargeableWeight = 1m,
            PackageCount = 1,
            InsuranceValue = 20000m,
            IsCod = true,
            CodAmount = 50000m
        }, 0m, 0);

        Assert.Equal(1000m, breakdown.InsuranceFee);
        Assert.Equal(2500m, breakdown.CodCommission);
    }

    [Fact]
    public void ManualCharge_ReplacesTheRuleSubtotal_ButKeepsTheCodCommission()
    {
        var rule = DemoStandard();
        rule.DiscountPercentage = 10m;

        var breakdown = PriceBreakdownCalculator.Calculate(rule, new PriceBreakdownInput
        {
            ChargeableWeight = 9m,
            PackageCount = 1,
            IsCod = true,
            CodAmount = 10000m,
            ManualShippingCharge = 20000m,
            ManualDiscountAmount = 1000m
        }, 0m, 0);

        Assert.Equal(20000m, breakdown.ShippingSubtotal);
        Assert.Equal(23000m, breakdown.CalculatedShippingSubtotal);
        Assert.Equal(0m, breakdown.ContractDiscountAmount);   // no contract discount on a manual price
        Assert.Equal(1000m, breakdown.ManualDiscountAmount);
        Assert.Equal(2100m, breakdown.CodCommission);
        Assert.Equal(21100m, breakdown.NetTotal);
    }

    [Fact]
    public void Amounts_AreRoundedToTheCurrencyDecimals()
    {
        var rule = Rule(1);
        rule.BasePrice = 10m;
        rule.WeightTiers.Add(new PriceRuleWeightTier { FromWeightKg = 0m, PricePerKg = 0.333m });

        var breakdown = PriceBreakdownCalculator.Calculate(rule, new PriceBreakdownInput { ChargeableWeight = 2.5m, PackageCount = 1 }, 15m, 2);

        Assert.Equal(0.83m, breakdown.WeightCharge);          // 0.8325
        Assert.Equal(10.83m, breakdown.ShippingSubtotal);
        Assert.Equal(1.62m, breakdown.TaxAmount);             // 1.6245
        Assert.Equal(12.45m, breakdown.NetTotal);
    }

    [Fact]
    public void WithoutRuleAndWithoutManualCharge_TheEngineRefuses()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PriceBreakdownCalculator.Calculate(null, new PriceBreakdownInput { ChargeableWeight = 1m, PackageCount = 1 }, 0m, 0));
    }

    // ------------------------------------------------------------------ step 9: weight or volume

    [Fact]
    public void RuleWithAppliesToBasis_OnlyMatchesShipmentsPricedThatWay_AndIsMoreSpecific()
    {
        var any = Rule(1);
        var perCbm = Rule(2);
        perCbm.AppliesToBasis = PricingBasis.Volume;

        var weightContext = new PricingContext { ChargeableWeight = 3m, PackageCount = 1, PricingBasis = PricingBasis.Weight };
        var volumeContext = new PricingContext { ChargeableWeight = 3m, PackageCount = 1, PricingBasis = PricingBasis.Volume };

        Assert.False(PriceRuleMatcher.Matches(perCbm, weightContext, Now));
        Assert.True(PriceRuleMatcher.Matches(perCbm, volumeContext, Now));
        Assert.Same(any, PriceRuleMatcher.SelectBest(new[] { any, perCbm }, weightContext, Now));
        Assert.Same(perCbm, PriceRuleMatcher.SelectBest(new[] { any, perCbm }, volumeContext, Now));
    }

    [Fact]
    public void Volume_ChargesTheCubicMetres_AndIgnoresTheWeightTiers()
    {
        var rule = DemoStandard();
        rule.PricePerCubicMeter = 95m;

        // 2.5 m³ x 95 = 237.5, plus the base price; the 40 kg would cost tiers in weight pricing.
        var breakdown = PriceBreakdownCalculator.Calculate(rule, new PriceBreakdownInput
        {
            ChargeableWeight = 40m,
            PackageCount = 1,
            PricingBasis = PricingBasis.Volume,
            VolumeCbm = 2.5m
        }, 0m, 2);

        Assert.Equal(237.5m, breakdown.VolumeCharge);
        Assert.Equal(0m, breakdown.WeightCharge);
        Assert.Empty(breakdown.WeightTiers);
        Assert.Equal(15000m + 237.5m, breakdown.ShippingSubtotal);

        var byWeight = PriceBreakdownCalculator.Calculate(rule, new PriceBreakdownInput
        {
            ChargeableWeight = 40m,
            PackageCount = 1,
            PricingBasis = PricingBasis.Weight,
            VolumeCbm = 2.5m
        }, 0m, 2);

        Assert.Equal(0m, byWeight.VolumeCharge);
        Assert.True(byWeight.WeightCharge > 0m);
    }

    [Fact]
    public void PerKgOnly_FourDinarsAKilo()
    {
        var rule = Rule(1);
        rule.WeightTiers.Add(new PriceRuleWeightTier { FromWeightKg = 0m, PricePerKg = 4m });

        var breakdown = PriceBreakdownCalculator.Calculate(rule, new PriceBreakdownInput { ChargeableWeight = 12.5m, PackageCount = 3 }, 0m, 3);

        Assert.Equal(50m, breakdown.ShippingSubtotal);
    }
}

