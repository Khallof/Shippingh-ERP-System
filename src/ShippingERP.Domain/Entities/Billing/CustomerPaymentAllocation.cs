using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.Billing;

/// <summary>
/// The part of a payment applied to one invoice. Append-only: allocations are never changed.
/// AllocatedAtUtc is used by the aging report "as of" a past date.
/// </summary>
public class CustomerPaymentAllocation : BaseEntity, IAppendOnlyEntity
{
    public long PaymentId { get; set; }

    public CustomerPayment Payment { get; set; } = null!;

    public long InvoiceId { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateTime AllocatedAtUtc { get; set; }
}
