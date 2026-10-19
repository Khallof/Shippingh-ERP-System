using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Customers;

// ------------------------------------------------------------------ pricing plans

public sealed class PricingPlanDto : MasterDataDto
{
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    /// <summary>Set when the plan is a customer-specific contract.</summary>
    public long? ContractCustomerId { get; init; }
    public string? ContractCustomerName { get; init; }
    public string? ContractReference { get; init; }
    public DateTime? ValidFromUtc { get; init; }
    public DateTime? ValidToUtc { get; init; }
    public string? Notes { get; init; }
    public int RuleCount { get; init; }
    public int CustomerCount { get; init; }
}

/// <summary>
/// Body of POST/PUT /api/v1/pricing-plans.
/// ContractCustomerId makes the plan a customer contract: it prices that customer's shipments before the plan
/// assigned to the customer and before the default plan (a contract plan cannot be the default plan).
/// </summary>
public sealed class SavePricingPlanRequest
{
    public string Code { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
    public long CurrencyId { get; set; }
    public bool IsDefault { get; set; }
    public long? ContractCustomerId { get; set; }
    public string? ContractReference { get; set; }
    public DateTime? ValidFromUtc { get; set; }
    public DateTime? ValidToUtc { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Search: code, names, contract reference. SortBy: code (default), arabicName, createdAtUtc.</summary>
public sealed class PricingPlanListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "code", "arabicName", "createdAtUtc" };

    public long? CurrencyId { get; set; }

    /// <summary>true = only customer contracts, false = only general plans.</summary>
    public bool? IsContract { get; set; }

    public long? ContractCustomerId { get; set; }
}

// ------------------------------------------------------------------ price rules

public sealed class PriceRuleConditionDto
{
    public long Id { get; init; }
    public PriceConditionType ConditionType { get; init; }
    public string ConditionTypeName { get; init; } = string.Empty;
    public long? ReferenceId { get; init; }
    /// <summary>Name of the zone / city / service type.</summary>
    public string? ReferenceName { get; init; }
    public decimal? MinValue { get; init; }
    public decimal? MaxValue { get; init; }
}

public sealed class PriceRuleWeightTierDto
{
    public long Id { get; init; }
    public decimal FromWeightKg { get; init; }
    public decimal? ToWeightKg { get; init; }
    public decimal PricePerKg { get; init; }
}

public sealed class PricingRuleDto
{
    public long Id { get; init; }
    public long PricingPlanId { get; init; }
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public int Priority { get; init; }
    public DateTime? ValidFromUtc { get; init; }
    public DateTime? ValidToUtc { get; init; }
    public decimal BasePrice { get; init; }
    public decimal IncludedWeightKg { get; init; }
    public decimal PricePerCubicMeter { get; init; }
    public PricingBasis? AppliesToBasis { get; init; }
    public int IncludedPackageCount { get; init; }
    public decimal PerPackageFee { get; init; }
    public decimal CodFeeFixed { get; init; }
    public decimal CodFeePercentage { get; init; }
    public decimal CodFeeMinimum { get; init; }
    public decimal InsuranceFeePercentage { get; init; }
    public decimal InsuranceFeeMinimum { get; init; }
    public decimal PickupFee { get; init; }
    public decimal PackagingFee { get; init; }
    public decimal RemoteAreaFee { get; init; }
    public decimal ReturnFee { get; init; }
    public decimal DiscountPercentage { get; init; }
    public long? TaxRateId { get; init; }
    public string? TaxRateName { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<PriceRuleConditionDto> Conditions { get; init; } = Array.Empty<PriceRuleConditionDto>();
    public IReadOnlyList<PriceRuleWeightTierDto> WeightTiers { get; init; } = Array.Empty<PriceRuleWeightTierDto>();
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

public sealed class SavePriceRuleConditionRequest
{
    public PriceConditionType ConditionType { get; set; }

    /// <summary>Zone / city / service type id (reference types only).</summary>
    public long? ReferenceId { get; set; }

    /// <summary>Inclusive (weight in kg, or package count). Range types only.</summary>
    public decimal? MinValue { get; set; }

    /// <summary>Exclusive. Empty = no limit. Range types only.</summary>
    public decimal? MaxValue { get; set; }
}

public sealed class SavePriceRuleWeightTierRequest
{
    public decimal FromWeightKg { get; set; }

    /// <summary>Empty = no upper limit (last tier only).</summary>
    public decimal? ToWeightKg { get; set; }

    public decimal PricePerKg { get; set; }
}

/// <summary>
/// Body of POST/PUT /api/v1/pricing-plans/{planId}/rules.
///
/// Conditions decide WHEN the rule applies (empty = every shipment); the amounts decide WHAT it charges.
/// Example "Damascus standard, up to 30 kg":
///   conditions: [{ conditionType: "ServiceType", referenceId: 1 }, { conditionType: "DestinationCity", referenceId: 1 },
///                { conditionType: "ChargeableWeight", minValue: 0, maxValue: 30 }]
///   basePrice: 15000, includedWeightKg: 5, weightTiers: [{ fromWeightKg: 5, toWeightKg: null, pricePerKg: 2000 }]
/// </summary>
public sealed class SavePricingRuleRequest
{
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }

    /// <summary>0 to 1000. Higher wins when several rules match.</summary>
    public int Priority { get; set; }

    public DateTime? ValidFromUtc { get; set; }
    public DateTime? ValidToUtc { get; set; }

    public List<SavePriceRuleConditionRequest> Conditions { get; set; } = new();

    public decimal BasePrice { get; set; }

    /// <summary>Weight covered by the base price.</summary>
    public decimal IncludedWeightKg { get; set; }

    /// <summary>Progressive per-kg prices above IncludedWeightKg. Contiguous; only the last may be open-ended.</summary>
    public List<SavePriceRuleWeightTierRequest> WeightTiers { get; set; } = new();

    /// <summary>Step 9: price of one m³ for shipments priced by volume.</summary>
    public decimal PricePerCubicMeter { get; set; }

    /// <summary>Step 9: null = both; Weight / Volume = only shipments priced that way.</summary>
    public PricingBasis? AppliesToBasis { get; set; }

    public int IncludedPackageCount { get; set; } = 1;
    public decimal PerPackageFee { get; set; }

    public decimal CodFeeFixed { get; set; }
    /// <summary>0 to 100.</summary>
    public decimal CodFeePercentage { get; set; }
    public decimal CodFeeMinimum { get; set; }

    /// <summary>0 to 100 (of the insurance value).</summary>
    public decimal InsuranceFeePercentage { get; set; }
    public decimal InsuranceFeeMinimum { get; set; }

    public decimal PickupFee { get; set; }
    public decimal PackagingFee { get; set; }
    public decimal RemoteAreaFee { get; set; }
    public decimal ReturnFee { get; set; }

    /// <summary>Contract discount 0 to 100 % on the shipping subtotal.</summary>
    public decimal DiscountPercentage { get; set; }

    /// <summary>Empty = the default tax rate.</summary>
    public long? TaxRateId { get; set; }

    public string? Notes { get; set; }
}
