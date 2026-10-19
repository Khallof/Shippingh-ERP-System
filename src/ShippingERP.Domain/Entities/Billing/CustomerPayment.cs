using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.MasterData;

namespace ShippingERP.Domain.Entities.Billing;

/// <summary>
/// Money received from a customer (سند قبض), number PAY-2026-000001: cash, bank transfer, card or another method.
///
/// The amount can be allocated to one or several open invoices of the same customer and currency, fully or
/// partially. What is not allocated (UnallocatedAmount) stays as customer credit and can be allocated later.
/// A payment is never edited or deleted (reversals come with accounting in the next phase).
///
/// IdempotencyKey: the client's "Idempotency-Key" header. Sending the same request twice (network retry, double
/// click) returns the first payment instead of recording the money twice. RequestHash detects a key reused with
/// a different body (409).
/// </summary>
public class CustomerPayment : BaseEntity
{
    public string PaymentNumber { get; set; } = string.Empty;

    public long CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public long PaymentMethodId { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = null!;

    public long CurrencyId { get; set; }

    public Currency Currency { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateTime PaymentDateUtc { get; set; }

    /// <summary>Bank transfer number, card receipt number... (required when the method requires it).</summary>
    public string? ReferenceNumber { get; set; }

    public string? Notes { get; set; }

    public decimal AllocatedAmount { get; set; }

    public decimal UnallocatedAmount { get; set; }

    public string? IdempotencyKey { get; set; }

    /// <summary>SHA-256 (hex) of the normalized request.</summary>
    public string? RequestHash { get; set; }

    public ICollection<CustomerPaymentAllocation> Allocations { get; set; } = new List<CustomerPaymentAllocation>();
}
