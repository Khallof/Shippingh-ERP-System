using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Customers;

/// <summary>
/// A customer: an individual or a business that sends and/or receives shipments.
/// The current balance is NOT stored here: it will be calculated from accounting entries in a later step.
/// </summary>
public class Customer : SoftDeletableEntity
{
    /// <summary>Unique code, generated automatically (CUS-00001) when not provided.</summary>
    public string Code { get; set; } = string.Empty;

    public CustomerType CustomerType { get; set; } = CustomerType.Individual;

    /// <summary>Sender, Recipient or Both.</summary>
    public CustomerRole Roles { get; set; } = CustomerRole.Both;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    public string Phone { get; set; } = string.Empty;

    public string? AlternatePhone { get; set; }

    public string? Email { get; set; }

    /// <summary>National id for individuals, commercial registration for businesses.</summary>
    public string? NationalOrCompanyNumber { get; set; }

    /// <summary>Maximum unpaid balance allowed (0 = cash only).</summary>
    public decimal CreditLimit { get; set; }

    /// <summary>Balance when the customer was created (positive = the customer owes us).</summary>
    public decimal OpeningBalance { get; set; }

    public string? Notes { get; set; }

    public long DefaultBranchId { get; set; }

    public MasterData.Branch DefaultBranch { get; set; } = null!;

    /// <summary>Price list used for this customer. Null = the default plan.</summary>
    public long? PricingPlanId { get; set; }

    public CustomerPricingPlan? PricingPlan { get; set; }

    public ICollection<CustomerAddress> Addresses { get; set; } = new List<CustomerAddress>();
}
