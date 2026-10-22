using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Entities.Pricing;
using ShippingERP.Domain.Entities.Settings;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Domain.Pricing;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Shipments;

/// <summary>Everything the price of a shipment depends on.</summary>
public sealed class PricingInput
{
    public long SenderCustomerId { get; init; }

    /// <summary>The plan assigned to the sender (Customer.PricingPlanId).</summary>
    public long? CustomerPricingPlanId { get; init; }

    public long? OriginZoneId { get; init; }
    public long? DestinationZoneId { get; init; }
    public long? OriginCityId { get; init; }
    public long? DestinationCityId { get; init; }
    public long ServiceTypeId { get; init; }
    public decimal ActualWeight { get; init; }
    public decimal VolumetricWeight { get; init; }
    public decimal ChargeableWeight { get; init; }
    public int PackageCount { get; init; }

    /// <summary>Step 9: by weight or by volume.</summary>
    public PricingBasis PricingBasis { get; init; } = PricingBasis.Weight;

    /// <summary>Step 9: total volume (m³, 3 decimals).</summary>
    public decimal VolumeCbm { get; init; }

    /// <summary>Step 9: price per kg / per m³ typed on the shipment (no rule needed). Null = the price rules.</summary>
    public decimal? UnitPrice { get; init; }

    /// <summary>Step 9: currency of UnitPrice; null = the base currency.</summary>
    public long? UnitPriceCurrencyId { get; init; }

    /// <summary>Step 9: extra service fees typed on the shipment.</summary>
    public decimal ExtraFee { get; init; }

    public bool IsCod { get; init; }
    public decimal CodAmount { get; init; }
    public decimal InsuranceValue { get; init; }
    public bool RequiresPickup { get; init; }
    public bool RequiresPackaging { get; init; }

    /// <summary>The shipment is being returned to the sender (return fee).</summary>
    public bool IsReturn { get; init; }

    public decimal? ManualShippingCharge { get; init; }
    public decimal ManualDiscountAmount { get; init; }

    public bool IsManual => ManualShippingCharge.HasValue || ManualDiscountAmount > 0m;
}

/// <summary>Result of a price calculation; becomes a ShipmentPricingSnapshot.</summary>
public sealed class PricingResult
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public PricingSource PricingSource { get; init; }
    public long? PricingPlanId { get; init; }
    public string? PricingPlanCode { get; init; }
    public long? PricingRuleId { get; init; }
    public string? PricingRuleName { get; init; }
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public int CurrencyDecimals { get; init; }
    public int VolumetricDivisor { get; init; }
    public decimal WeightRoundingStepKg { get; init; }
    public PricingInput Input { get; init; } = null!;
    public bool IsRemoteArea { get; init; }
    public long? TaxRateId { get; init; }
    public PriceBreakdown Breakdown { get; init; } = null!;

    /// <summary>The same shipment priced by the rule only (no manual values). Null when no rule matched.</summary>
    public PriceBreakdown? RuleBreakdown { get; init; }

    // Shortcuts used by the shipment.
    public decimal ShippingCharge => Breakdown.GrossCharge;
    public decimal DiscountAmount => Breakdown.DiscountAmount;
    public decimal TaxAmount => Breakdown.TaxAmount;
    public decimal TotalCharge => Breakdown.NetTotal;

    public ShipmentPricingSnapshot ToSnapshot(Shipment shipment, string reason, string? overrideReason = null, long? priceOverrideId = null)
    {
        var b = Breakdown;
        return new ShipmentPricingSnapshot
        {
            Shipment = shipment,
            Reason = reason,
            PricingSource = PricingSource,
            PricingPlanId = PricingPlanId,
            PricingPlanCode = PricingPlanCode,
            PricingRuleId = PricingRuleId,
            PricingRuleName = PricingRuleName,
            CurrencyId = CurrencyId,
            CurrencyCode = CurrencyCode,
            ActualWeight = Input.ActualWeight,
            VolumetricWeight = Input.VolumetricWeight,
            ChargeableWeight = Input.ChargeableWeight,
            VolumetricDivisor = VolumetricDivisor,
            WeightRoundingStepKg = WeightRoundingStepKg,
            PackageCount = Input.PackageCount,
            PricingBasis = b.PricingBasis,
            VolumeCbm = Input.VolumeCbm,
            PricePerCubicMeter = b.PricePerCubicMeter,
            VolumeCharge = b.VolumeCharge,
            InsuranceValue = Input.InsuranceValue,
            RequiresPickup = Input.RequiresPickup,
            RequiresPackaging = Input.RequiresPackaging,
            IsRemoteArea = IsRemoteArea,
            IsReturn = Input.IsReturn,
            BasePrice = b.BasePrice,
            IncludedWeightKg = b.IncludedWeightKg,
            AdditionalWeightKg = b.AdditionalWeightKg,
            AdditionalWeightCharge = b.WeightCharge,
            WeightTiersJson = b.WeightTiers.Count == 0 ? null : JsonSerializer.Serialize(b.WeightTiers, JsonOptions),
            PackageFee = b.PackageFee,
            InsuranceFee = b.InsuranceFee,
            PickupFee = b.PickupFee,
            PackagingFee = b.PackagingFee,
            RemoteAreaFee = b.RemoteAreaFee,
            ExtraFee = b.ExtraFee,
            ReturnFee = b.ReturnFee,
            ServiceFeesTotal = b.ServiceFeesTotal,
            CodFeeFixed = b.CodFeeFixed,
            CodFeePercentage = b.CodFeePercentage,
            CodFeeMinimum = b.CodFeeMinimum,
            CodAmount = b.CodAmount,
            CodFee = b.CodCommission,
            ShippingSubtotal = b.ShippingSubtotal,
            CalculatedShippingSubtotal = b.CalculatedShippingSubtotal,
            ContractDiscountPercentage = b.ContractDiscountPercentage,
            ContractDiscountAmount = b.ContractDiscountAmount,
            ManualDiscountAmount = b.ManualDiscountAmount,
            DiscountAmount = b.DiscountAmount,
            TotalShippingCharge = b.TotalShippingCharge,
            TaxableAmount = b.TaxableAmount,
            ShippingCharge = b.GrossCharge,
            TaxRateId = TaxRateId,
            TaxPercentage = b.TaxPercentage,
            TaxAmount = b.TaxAmount,
            TotalCharge = b.NetTotal,
            OverrideReason = overrideReason,
            PriceOverrideId = priceOverrideId
        };
    }

    public ShipmentPricingDto ToDto(string reason) => ToDto(Breakdown, PricingSource, reason);

    /// <summary>The rule-only breakdown as a DTO (null when no rule matched).</summary>
    public ShipmentPricingDto? ToRuleDto() =>
        RuleBreakdown is null
            ? null
            : ToDto(RuleBreakdown, PricingSource == PricingSource.Manual ? PricingSource.PricingRule : PricingSource, "Rule");

    private ShipmentPricingDto ToDto(PriceBreakdown b, PricingSource source, string reason) => new()
    {
        Reason = reason,
        PricingSource = source,
        PricingPlanId = PricingPlanId,
        PricingPlanCode = PricingPlanCode,
        PricingRuleId = PricingRuleId,
        PricingRuleName = PricingRuleName,
        CurrencyId = CurrencyId,
        CurrencyCode = CurrencyCode,
        ActualWeight = Input.ActualWeight,
        VolumetricWeight = Input.VolumetricWeight,
        ChargeableWeight = Input.ChargeableWeight,
        VolumetricDivisor = VolumetricDivisor,
        WeightRoundingStepKg = WeightRoundingStepKg,
        PackageCount = Input.PackageCount,
        PricingBasis = b.PricingBasis,
        VolumeCbm = Input.VolumeCbm,
        PricePerCubicMeter = b.PricePerCubicMeter,
        VolumeCharge = b.VolumeCharge,
        InsuranceValue = Input.InsuranceValue,
        RequiresPickup = Input.RequiresPickup,
        RequiresPackaging = Input.RequiresPackaging,
        IsRemoteArea = IsRemoteArea,
        IsReturn = Input.IsReturn,
        BaseCharge = b.BasePrice,
        IncludedWeightKg = b.IncludedWeightKg,
        AdditionalWeightKg = b.AdditionalWeightKg,
        WeightCharge = b.WeightCharge,
        WeightTiers = b.WeightTiers
            .Select(t => new PricingWeightTierDto { FromKg = t.FromKg, ToKg = t.ToKg, Kg = t.Kg, PricePerKg = t.PricePerKg, Charge = t.Charge })
            .ToList(),
        PackageFee = b.PackageFee,
        InsuranceFee = b.InsuranceFee,
        PickupFee = b.PickupFee,
        PackagingFee = b.PackagingFee,
        RemoteAreaFee = b.RemoteAreaFee,
        ExtraFee = b.ExtraFee,
        ReturnFee = b.ReturnFee,
        ServiceFeesTotal = b.ServiceFeesTotal,
        ShippingSubtotal = b.ShippingSubtotal,
        CalculatedShippingSubtotal = b.CalculatedShippingSubtotal,
        ContractDiscountPercentage = b.ContractDiscountPercentage,
        ContractDiscountAmount = b.ContractDiscountAmount,
        ManualDiscountAmount = b.ManualDiscountAmount,
        DiscountAmount = b.DiscountAmount,
        TotalShippingCharge = b.TotalShippingCharge,
        CodAmount = b.CodAmount,
        CodFeeFixed = b.CodFeeFixed,
        CodFeePercentage = b.CodFeePercentage,
        CodFeeMinimum = b.CodFeeMinimum,
        CodCommission = b.CodCommission,
        TaxableAmount = b.TaxableAmount,
        TaxRateId = TaxRateId,
        TaxPercentage = b.TaxPercentage,
        TaxAmount = b.TaxAmount,
        NetTotal = b.NetTotal,
        ShippingCharge = b.GrossCharge,
        TotalCharge = b.NetTotal
    };
}

/// <summary>How big a manual price change is, and whether it needs an approver.</summary>
public sealed record PriceOverrideEvaluation(bool IsManual, decimal? RuleTotalShippingCharge, decimal ProposedTotalShippingCharge, decimal? DeviationPercentage, bool RequiresApproval)
{
    /// <summary>
    /// Compares the total shipping charge (after discounts, before COD commission and tax) with and without the
    /// manual values. No rule (nothing to compare with) or a rule price of 0 always needs approval.
    /// </summary>
    public static PriceOverrideEvaluation From(PricingResult result, decimal thresholdPercent)
    {
        var proposed = result.Breakdown.TotalShippingCharge;
        if (!result.Input.IsManual)
        {
            return new PriceOverrideEvaluation(false, proposed, proposed, null, false);
        }

        var rule = result.RuleBreakdown?.TotalShippingCharge;
        decimal? deviation = rule is > 0m
            ? Math.Round(Math.Abs(proposed - rule.Value) / rule.Value * 100m, 2, MidpointRounding.AwayFromZero)
            : null;

        var requiresApproval = deviation is null || deviation.Value > thresholdPercent;
        return new PriceOverrideEvaluation(true, rule, proposed, deviation, requiresApproval);
    }
}

/// <summary>
/// The pricing engine (step 5). Loads the data, then delegates the arithmetic to the pure Domain classes
/// (PriceRuleMatcher, PriceBreakdownCalculator).
///
/// 1. Plans, in order: the sender's active and valid contract plan, the plan assigned to the sender, the default
///    plan, then (step 9) the other active general plans (not assigned to any customer) by id. The FIRST plan that has a matching rule prices the
///    shipment (a contract that does not cover this service / route falls back to the normal prices). So a "per kg"
///    plan in one currency and a "per m³" plan in another can live side by side.
/// 2. Rule: the best matching active rule of that plan (priority, specificity, oldest).
/// 3. Breakdown: base + weight tiers + service fees - discounts + COD commission + tax (see PriceRule).
/// 4. Manual values (a manual subtotal and/or a manual discount) replace / extend the rule price; the rule-only
///    price is kept in RuleBreakdown so the size of the change can be judged.
///
/// Plans, rules, zones, currencies and tax rates are cached for the lifetime of the scope (one HTTP request),
/// which keeps the Excel import fast.
/// </summary>
public sealed class ShipmentPricingCalculator
{
    private readonly AppDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    private readonly Dictionary<long, PlanInfo?> _plans = new();
    private readonly Dictionary<long, PlanInfo?> _contracts = new();
    private PlanInfo? _defaultPlan;
    private bool _defaultPlanLoaded;
    private List<PlanInfo>? _otherGeneralPlans;
    private CurrencyInfo? _baseCurrency;
    private readonly Dictionary<long, CurrencyInfo> _currencies = new();
    private readonly Dictionary<long, decimal?> _taxRates = new();
    private TaxInfo? _defaultTax;
    private bool _defaultTaxLoaded;
    private readonly Dictionary<long, bool> _remoteZones = new();

    public ShipmentPricingCalculator(AppDbContext dbContext, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<PricingResult> CalculateAsync(PricingInput input, ShippingSettings settings, CancellationToken cancellationToken)
    {
        if (input.UnitPrice.HasValue && !input.ManualShippingCharge.HasValue)
        {
            return await CalculateWithUnitPriceAsync(input, settings, cancellationToken);
        }

        var now = _dateTimeProvider.UtcNow;

        var context = new PricingContext
        {
            OriginZoneId = input.OriginZoneId,
            DestinationZoneId = input.DestinationZoneId,
            OriginCityId = input.OriginCityId,
            DestinationCityId = input.DestinationCityId,
            ServiceTypeId = input.ServiceTypeId,
            ChargeableWeight = input.ChargeableWeight,
            PackageCount = input.PackageCount,
            PricingBasis = input.PricingBasis
        };

        // The first plan (contract -> assigned -> default) that has a matching rule prices the shipment.
        // A contract that does not cover this kind of shipment therefore falls back to the normal prices.
        var candidates = await ResolvePlansAsync(input.SenderCustomerId, input.CustomerPricingPlanId, now, cancellationToken);
        PlanInfo? plan = null;
        PriceRule? rule = null;
        var source = PricingSource.PricingRule;

        foreach (var (candidate, candidateSource) in candidates)
        {
            var match = PriceRuleMatcher.SelectBest(candidate.Rules, context, now);
            if (match is not null)
            {
                (plan, rule, source) = (candidate, match, candidateSource);
                break;
            }
        }

        // No rule anywhere: a manual price uses the currency of the first applicable plan.
        if (plan is null && candidates.Count > 0)
        {
            (plan, source) = candidates[0];
        }

        var currency = plan is not null
            ? await GetCurrencyAsync(plan.CurrencyId, cancellationToken)
            : await GetBaseCurrencyAsync(cancellationToken);
        var decimals = Math.Clamp(currency.DecimalPlaces, 0, 4);

        if (rule is null && input.ManualShippingCharge is null)
        {
            throw new BusinessRuleException(
                $"لا توجد قاعدة تسعير مناسبة لهذه الشحنة ({ShipmentChargeEnums.Arabic(input.PricingBasis)}، نوع البضاعة، المناطق، الوزن، عدد الطرود). " +
                "أضف قاعدة في خطة التسعير، أو أدخل سعراً يدوياً بواسطة مستخدم يملك صلاحية Shipments.OverridePrice.");
        }

        var isRemoteArea = input.DestinationZoneId.HasValue && await IsRemoteZoneAsync(input.DestinationZoneId.Value, cancellationToken);
        var (taxRateId, taxPercentage) = await ResolveTaxAsync(rule?.TaxRateId, cancellationToken);

        var breakdownInput = new PriceBreakdownInput
        {
            ChargeableWeight = input.ChargeableWeight,
            PricingBasis = input.PricingBasis,
            VolumeCbm = input.VolumeCbm,
            PackageCount = input.PackageCount,
            IsCod = input.IsCod,
            CodAmount = input.IsCod ? input.CodAmount : 0m,
            InsuranceValue = input.InsuranceValue,
            RequiresPickup = input.RequiresPickup,
            RequiresPackaging = input.RequiresPackaging,
            IsRemoteArea = isRemoteArea,
            IsReturn = input.IsReturn,
            ExtraFee = input.ExtraFee,
            ManualShippingCharge = input.ManualShippingCharge,
            ManualDiscountAmount = input.ManualDiscountAmount
        };

        var breakdown = PriceBreakdownCalculator.Calculate(rule, breakdownInput, taxPercentage, decimals);

        if (breakdown.DiscountAmount > breakdown.ShippingSubtotal)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["discountAmount"] = new[] { $"الخصم لا يمكن أن يزيد عن سعر الشحن ({breakdown.ShippingSubtotal - breakdown.ContractDiscountAmount})." }
            });
        }

        var ruleBreakdown = rule is null
            ? null
            : input.IsManual
                ? PriceBreakdownCalculator.Calculate(rule, new PriceBreakdownInput
                {
                    ChargeableWeight = breakdownInput.ChargeableWeight,
                    PricingBasis = breakdownInput.PricingBasis,
                    VolumeCbm = breakdownInput.VolumeCbm,
                    PackageCount = breakdownInput.PackageCount,
                    IsCod = breakdownInput.IsCod,
                    CodAmount = breakdownInput.CodAmount,
                    InsuranceValue = breakdownInput.InsuranceValue,
                    RequiresPickup = breakdownInput.RequiresPickup,
                    RequiresPackaging = breakdownInput.RequiresPackaging,
                    IsRemoteArea = breakdownInput.IsRemoteArea,
                    IsReturn = breakdownInput.IsReturn,
                    ExtraFee = breakdownInput.ExtraFee
                }, taxPercentage, decimals)
                : breakdown;

        return new PricingResult
        {
            PricingSource = input.ManualShippingCharge.HasValue ? PricingSource.Manual : source,
            PricingPlanId = plan?.Id,
            PricingPlanCode = plan?.Code,
            PricingRuleId = rule?.Id,
            PricingRuleName = rule?.ArabicName,
            CurrencyId = currency.Id,
            CurrencyCode = currency.Code,
            CurrencyDecimals = decimals,
            VolumetricDivisor = settings.VolumetricDivisor,
            WeightRoundingStepKg = settings.WeightRoundingStepKg,
            Input = input,
            IsRemoteArea = isRemoteArea,
            TaxRateId = taxRateId,
            Breakdown = breakdown,
            RuleBreakdown = ruleBreakdown
        };
    }

    // ------------------------------------------------------------------ unit price (step 9)

    /// <summary>
    /// Price typed on the shipment: chargeable weight x price per kg, or volume x price per m³, in the chosen currency
    /// (base currency by default). Implemented as an in-memory rule so the breakdown, discount and tax work exactly
    /// like a rule price. No plan or rule is needed and no approval: it is the normal price of the shipment.
    /// </summary>
    private async Task<PricingResult> CalculateWithUnitPriceAsync(PricingInput input, ShippingSettings settings, CancellationToken cancellationToken)
    {
        var unitPrice = input.UnitPrice!.Value;
        var currency = input.UnitPriceCurrencyId.HasValue
            ? await GetCurrencyAsync(input.UnitPriceCurrencyId.Value, cancellationToken)
            : await GetBaseCurrencyAsync(cancellationToken);
        var decimals = Math.Clamp(currency.DecimalPlaces, 0, 4);
        var byVolume = input.PricingBasis == PricingBasis.Volume;

        var rule = new PriceRule
        {
            ArabicName = string.Create(CultureInfo.InvariantCulture, $"سعر {(byVolume ? "المتر المكعب" : "الكيلو")} {unitPrice:0.####}"),
            IsActive = true,
            IncludedWeightKg = 0m,
            IncludedPackageCount = 1,
            PricePerCubicMeter = byVolume ? unitPrice : 0m
        };
        if (!byVolume)
        {
            rule.WeightTiers.Add(new PriceRuleWeightTier { FromWeightKg = 0m, ToWeightKg = null, PricePerKg = unitPrice });
        }

        var isRemoteArea = input.DestinationZoneId.HasValue && await IsRemoteZoneAsync(input.DestinationZoneId.Value, cancellationToken);
        var (taxRateId, taxPercentage) = await ResolveTaxAsync(null, cancellationToken);

        PriceBreakdownInput Input(decimal discount) => new()
        {
            ChargeableWeight = input.ChargeableWeight,
            PricingBasis = input.PricingBasis,
            VolumeCbm = input.VolumeCbm,
            PackageCount = input.PackageCount,
            IsCod = input.IsCod,
            CodAmount = input.IsCod ? input.CodAmount : 0m,
            InsuranceValue = input.InsuranceValue,
            RequiresPickup = input.RequiresPickup,
            RequiresPackaging = input.RequiresPackaging,
            IsRemoteArea = isRemoteArea,
            IsReturn = input.IsReturn,
            ExtraFee = input.ExtraFee,
            ManualDiscountAmount = discount
        };

        var breakdown = PriceBreakdownCalculator.Calculate(rule, Input(input.ManualDiscountAmount), taxPercentage, decimals);
        if (breakdown.DiscountAmount > breakdown.ShippingSubtotal)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["discountAmount"] = new[] { $"الخصم لا يمكن أن يزيد عن سعر الشحن ({breakdown.ShippingSubtotal})." }
            });
        }

        return new PricingResult
        {
            PricingSource = PricingSource.UnitPrice,
            PricingRuleName = rule.ArabicName,
            CurrencyId = currency.Id,
            CurrencyCode = currency.Code,
            CurrencyDecimals = decimals,
            VolumetricDivisor = settings.VolumetricDivisor,
            WeightRoundingStepKg = settings.WeightRoundingStepKg,
            Input = input,
            IsRemoteArea = isRemoteArea,
            TaxRateId = taxRateId,
            Breakdown = breakdown,
            // The typed unit price is the reference: only a manual discount on top of it is a "manual change".
            RuleBreakdown = input.ManualDiscountAmount > 0m
                ? PriceBreakdownCalculator.Calculate(rule, Input(0m), taxPercentage, decimals)
                : breakdown
        };
    }

    // ------------------------------------------------------------------ plan

    /// <summary>Applicable plans in priority order: the sender's contract, the assigned plan, the default plan.</summary>
    private async Task<List<(PlanInfo Plan, PricingSource Source)>> ResolvePlansAsync(
        long senderId,
        long? assignedPlanId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var result = new List<(PlanInfo Plan, PricingSource Source)>();

        if (!_contracts.TryGetValue(senderId, out var contract))
        {
            var contractId = await _dbContext.CustomerPricingPlans
                .AsNoTracking()
                .Where(x => x.ContractCustomerId == senderId && x.IsActive &&
                            (x.ValidFromUtc == null || x.ValidFromUtc <= now) &&
                            (x.ValidToUtc == null || x.ValidToUtc > now))
                .OrderByDescending(x => x.ValidFromUtc)
                .ThenByDescending(x => x.Id)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            contract = contractId.HasValue ? await GetPlanAsync(contractId.Value, cancellationToken) : null;
            _contracts[senderId] = contract;
        }

        if (contract is not null && IsValid(contract, now))
        {
            result.Add((contract, PricingSource.CustomerContract));
        }

        if (assignedPlanId.HasValue && result.All(x => x.Plan.Id != assignedPlanId.Value))
        {
            var assigned = await GetPlanAsync(assignedPlanId.Value, cancellationToken);
            if (assigned is not null && IsValid(assigned, now) && (assigned.ContractCustomerId is null || assigned.ContractCustomerId == senderId))
            {
                result.Add((assigned, assigned.ContractCustomerId.HasValue ? PricingSource.CustomerContract : PricingSource.PricingRule));
            }
        }

        if (!_defaultPlanLoaded)
        {
            var defaultId = await _dbContext.CustomerPricingPlans
                .AsNoTracking()
                .Where(x => x.IsDefault && x.IsActive)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            _defaultPlan = defaultId.HasValue ? await GetPlanAsync(defaultId.Value, cancellationToken) : null;
            _defaultPlanLoaded = true;
        }

        if (_defaultPlan is not null && IsValid(_defaultPlan, now) && result.All(x => x.Plan.Id != _defaultPlan.Id))
        {
            result.Add((_defaultPlan, PricingSource.PricingRule));
        }

        // Step 9: the other general plans (no contract, not assigned to any customer, so special prices never leak),
        // e.g. a per-m³ plan in USD next to the default per-kg plan in LYD.
        if (_otherGeneralPlans is null)
        {
            var ids = await _dbContext.CustomerPricingPlans
                .AsNoTracking()
                .Where(x => x.ContractCustomerId == null && !x.IsDefault && x.IsActive &&
                            !_dbContext.Customers.Any(c => c.PricingPlanId == x.Id))
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            _otherGeneralPlans = new List<PlanInfo>();
            foreach (var id in ids)
            {
                var other = await GetPlanAsync(id, cancellationToken);
                if (other is not null)
                {
                    _otherGeneralPlans.Add(other);
                }
            }
        }

        foreach (var other in _otherGeneralPlans)
        {
            if (IsValid(other, now) && result.All(x => x.Plan.Id != other.Id))
            {
                result.Add((other, PricingSource.PricingRule));
            }
        }

        return result;
    }

    private static bool IsValid(PlanInfo plan, DateTime now) =>
        plan.IsActive &&
        (plan.ValidFromUtc is null || plan.ValidFromUtc <= now) &&
        (plan.ValidToUtc is null || plan.ValidToUtc > now);

    private async Task<PlanInfo?> GetPlanAsync(long planId, CancellationToken cancellationToken)
    {
        if (_plans.TryGetValue(planId, out var cached))
        {
            return cached;
        }

        var plan = await _dbContext.CustomerPricingPlans
            .AsNoTracking()
            .Where(x => x.Id == planId)
            .Select(x => new PlanInfo(x.Id, x.Code, x.CurrencyId, x.IsActive, x.ValidFromUtc, x.ValidToUtc, x.ContractCustomerId))
            .FirstOrDefaultAsync(cancellationToken);

        if (plan is not null)
        {
            plan.Rules = await _dbContext.PriceRules
                .AsNoTracking()
                .Include(x => x.Conditions)
                .Include(x => x.WeightTiers)
                .Where(x => x.PricingPlanId == planId && x.IsActive)
                .ToListAsync(cancellationToken);
        }

        _plans[planId] = plan;
        return plan;
    }

    // ------------------------------------------------------------------ reference data

    private async Task<bool> IsRemoteZoneAsync(long zoneId, CancellationToken cancellationToken)
    {
        if (!_remoteZones.TryGetValue(zoneId, out var isRemote))
        {
            isRemote = await _dbContext.Zones
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(x => x.Id == zoneId)
                .Select(x => x.IsRemoteArea)
                .FirstOrDefaultAsync(cancellationToken);
            _remoteZones[zoneId] = isRemote;
        }

        return isRemote;
    }

    /// <summary>The rule's tax rate when set (and not deleted), otherwise the default tax rate (or 0 %).</summary>
    private async Task<(long? TaxRateId, decimal Percentage)> ResolveTaxAsync(long? ruleTaxRateId, CancellationToken cancellationToken)
    {
        if (ruleTaxRateId.HasValue)
        {
            if (!_taxRates.TryGetValue(ruleTaxRateId.Value, out var percentage))
            {
                var id = ruleTaxRateId.Value;
                percentage = await _dbContext.TaxRates
                    .AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => (decimal?)x.Percentage)
                    .FirstOrDefaultAsync(cancellationToken);
                _taxRates[ruleTaxRateId.Value] = percentage;
            }

            if (percentage.HasValue)
            {
                return (ruleTaxRateId, percentage.Value);
            }
        }

        if (!_defaultTaxLoaded)
        {
            _defaultTax = await _dbContext.TaxRates
                .AsNoTracking()
                .Where(x => x.IsDefault && x.IsActive)
                .Select(x => new TaxInfo(x.Id, x.Percentage))
                .FirstOrDefaultAsync(cancellationToken);
            _defaultTaxLoaded = true;
        }

        return (_defaultTax?.Id, _defaultTax?.Percentage ?? 0m);
    }

    private async Task<CurrencyInfo> GetCurrencyAsync(long currencyId, CancellationToken cancellationToken)
    {
        if (_currencies.TryGetValue(currencyId, out var cached))
        {
            return cached;
        }

        var currency = await _dbContext.Currencies
                           .AsNoTracking()
                           .IgnoreQueryFilters()
                           .Where(x => x.Id == currencyId)
                           .Select(x => new CurrencyInfo(x.Id, x.Code, x.DecimalPlaces))
                           .FirstOrDefaultAsync(cancellationToken)
                       ?? throw new BusinessRuleException("عملة خطة التسعير غير موجودة.");

        _currencies[currencyId] = currency;
        return currency;
    }

    private async Task<CurrencyInfo> GetBaseCurrencyAsync(CancellationToken cancellationToken)
    {
        if (_baseCurrency is not null)
        {
            return _baseCurrency;
        }

        var baseCurrencyId = await _dbContext.Currencies
            .AsNoTracking()
            .Where(x => x.IsBaseCurrency && x.IsActive)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        baseCurrencyId ??= await _dbContext.CompanyProfiles
            .AsNoTracking()
            .Select(x => (long?)x.BaseCurrencyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (baseCurrencyId is null)
        {
            throw new BusinessRuleException("لا توجد عملة أساسية. عرّف العملة الأساسية في البيانات الأساسية أولاً.");
        }

        _baseCurrency = await GetCurrencyAsync(baseCurrencyId.Value, cancellationToken);
        return _baseCurrency;
    }

    private sealed record PlanInfo(long Id, string Code, long CurrencyId, bool IsActive, DateTime? ValidFromUtc, DateTime? ValidToUtc, long? ContractCustomerId)
    {
        public List<PriceRule> Rules { get; set; } = new();
    }

    private sealed record CurrencyInfo(long Id, string Code, int DecimalPlaces);

    private sealed record TaxInfo(long Id, decimal Percentage);
}
