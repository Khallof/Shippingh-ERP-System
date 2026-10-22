using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Shipments;

/// <summary>
/// A frozen, complete copy of how the price of a shipment was calculated: the plan and rule, every input
/// (weights, packages, COD, insurance, optional services), every component of the breakdown and the tax.
/// Later changes to plans, rules, tax rates or settings never change the price of an existing shipment.
///
/// Append-only: a new snapshot is added every time the shipment is priced (Created, Updated, Imported, Repriced,
/// ManualOverride). The latest one is the current price; invoice lines point to the snapshot they billed.
///
/// Breakdown:
///   ShippingSubtotal     = BasePrice + AdditionalWeightCharge + ServiceFeesTotal      (or the manual charge)
///   DiscountAmount       = ContractDiscountAmount + ManualDiscountAmount
///   TotalShippingCharge  = ShippingSubtotal - DiscountAmount
///   TaxableAmount        = TotalShippingCharge + CodFee (COD commission)
///   TotalCharge          = TaxableAmount + TaxAmount                                  (net total)
///   ShippingCharge       = ShippingSubtotal + CodFee                                  (gross, as on the shipment)
/// </summary>
public class ShipmentPricingSnapshot : BaseEntity, IAppendOnlyEntity
{
    public long ShipmentId { get; set; }

    public Shipment Shipment { get; set; } = null!;

    /// <summary>"Created", "Updated", "Imported", "Repriced" or "ManualOverride".</summary>
    public string Reason { get; set; } = string.Empty;

    public PricingSource PricingSource { get; set; }

    public long? PricingPlanId { get; set; }

    public string? PricingPlanCode { get; set; }

    /// <summary>The PriceRule used (no foreign key: snapshots made in step 4 refer to the old rule table).</summary>
    public long? PricingRuleId { get; set; }

    public string? PricingRuleName { get; set; }

    public long CurrencyId { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    // ---------------- Inputs ----------------

    public decimal ActualWeight { get; set; }

    public decimal VolumetricWeight { get; set; }

    public decimal ChargeableWeight { get; set; }

    public int VolumetricDivisor { get; set; }

    public decimal WeightRoundingStepKg { get; set; }

    public int PackageCount { get; set; }

    /// <summary>Step 9: by weight or by volume.</summary>
    public PricingBasis PricingBasis { get; set; } = PricingBasis.Weight;

    /// <summary>Step 9: the volume priced (m³, 3 decimals).</summary>
    public decimal VolumeCbm { get; set; }

    public decimal PricePerCubicMeter { get; set; }

    /// <summary>VolumeCbm x PricePerCubicMeter (0 for weight pricing).</summary>
    public decimal VolumeCharge { get; set; }

    public decimal InsuranceValue { get; set; }

    public bool RequiresPickup { get; set; }

    public bool RequiresPackaging { get; set; }

    public bool IsRemoteArea { get; set; }

    public bool IsReturn { get; set; }

    // ---------------- Base and weight ----------------

    public decimal BasePrice { get; set; }

    /// <summary>Weight covered by the base price.</summary>
    public decimal IncludedWeightKg { get; set; }

    /// <summary>Kilograms charged by the weight tiers.</summary>
    public decimal AdditionalWeightKg { get; set; }

    /// <summary>Weight charge (sum of the tiers).</summary>
    public decimal AdditionalWeightCharge { get; set; }

    /// <summary>The tiers applied, as JSON: [{"fromKg":5,"toKg":20,"kg":3,"pricePerKg":2000,"charge":6000}].</summary>
    public string? WeightTiersJson { get; set; }

    // ---------------- Service fees ----------------

    public decimal PackageFee { get; set; }

    public decimal InsuranceFee { get; set; }

    public decimal PickupFee { get; set; }

    public decimal PackagingFee { get; set; }

    public decimal RemoteAreaFee { get; set; }

    public decimal ReturnFee { get; set; }

    /// <summary>Step 9: extra service fees typed on the shipment (part of ServiceFeesTotal).</summary>
    public decimal ExtraFee { get; set; }

    public decimal ServiceFeesTotal { get; set; }

    // ---------------- COD commission ----------------

    public decimal CodFeeFixed { get; set; }

    public decimal CodFeePercentage { get; set; }

    public decimal CodFeeMinimum { get; set; }

    public decimal CodAmount { get; set; }

    /// <summary>COD commission.</summary>
    public decimal CodFee { get; set; }

    // ---------------- Totals ----------------

    /// <summary>Base + weight + service fees, or the manual charge.</summary>
    public decimal ShippingSubtotal { get; set; }

    /// <summary>The rule price when a manual charge was used (null when no rule matched or no manual charge).</summary>
    public decimal? CalculatedShippingSubtotal { get; set; }

    public decimal ContractDiscountPercentage { get; set; }

    public decimal ContractDiscountAmount { get; set; }

    public decimal ManualDiscountAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalShippingCharge { get; set; }

    public decimal TaxableAmount { get; set; }

    /// <summary>Gross charge stored on the shipment: ShippingSubtotal + COD commission.</summary>
    public decimal ShippingCharge { get; set; }

    public long? TaxRateId { get; set; }

    public decimal TaxPercentage { get; set; }

    public decimal TaxAmount { get; set; }

    /// <summary>Net total: TaxableAmount + TaxAmount.</summary>
    public decimal TotalCharge { get; set; }

    // ---------------- Manual override ----------------

    public string? OverrideReason { get; set; }

    public long? PriceOverrideId { get; set; }
}
