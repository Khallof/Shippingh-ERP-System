using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Pricing;

namespace ShippingERP.Domain.Entities.Customers;

/// <summary>
/// A price list (خطة تسعير). The prices are in its <see cref="Rules"/> (PriceRule).
///
/// Which plan prices a shipment (first match wins, each must be active and valid today):
/// 1. A customer contract: a plan whose ContractCustomerId is the sender (عقد عميل).
/// 2. The plan assigned to the sender (Customer.PricingPlanId).
/// 3. The default plan (exactly one plan is the default).
/// </summary>
public class CustomerPricingPlan : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    /// <summary>All prices of the plan are in this currency.</summary>
    public long CurrencyId { get; set; }

    public MasterData.Currency Currency { get; set; } = null!;

    /// <summary>Only one plan can be the default. A contract plan can never be the default.</summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Optional: makes this plan a customer-specific contract. It is used for that customer's shipments before any
    /// other plan, while it is active and valid.
    /// </summary>
    public long? ContractCustomerId { get; set; }

    public Customer? ContractCustomer { get; set; }

    /// <summary>Optional contract reference (e.g. the signed contract number).</summary>
    public string? ContractReference { get; set; }

    public DateTime? ValidFromUtc { get; set; }

    public DateTime? ValidToUtc { get; set; }

    public string? Notes { get; set; }

    public ICollection<PriceRule> Rules { get; set; } = new List<PriceRule>();
}
