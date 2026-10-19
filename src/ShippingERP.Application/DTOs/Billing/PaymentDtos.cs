using ShippingERP.Application.DTOs.Common;

namespace ShippingERP.Application.DTOs.Billing;

/// <summary>Implemented by the requests that allocate money to invoices (shared validation rules).</summary>
public interface IPaymentAllocationRequest
{
    bool AutoAllocate { get; }

    List<PaymentAllocationRequest> Allocations { get; }
}

public sealed class PaymentAllocationRequest
{
    public long InvoiceId { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>
/// Body of POST /api/v1/customer-payments. Send the header "Idempotency-Key: &lt;unique value&gt;" (e.g. a GUID made
/// by the client) so a retried request never records the same money twice.
///
/// Allocation: give Allocations (invoice + amount, partial amounts allowed), or AutoAllocate = true to pay the
/// oldest open invoices first. The total allocated cannot exceed Amount; the rest stays as customer credit.
/// </summary>
public sealed class CreateCustomerPaymentRequest : IPaymentAllocationRequest
{
    public long CustomerId { get; set; }

    /// <summary>Receiving branch. Default: the caller's branch.</summary>
    public long? BranchId { get; set; }

    /// <summary>Cash, bank transfer, card... (not "cash on delivery" or "customer account").</summary>
    public long PaymentMethodId { get; set; }

    /// <summary>Default: the base currency.</summary>
    public long? CurrencyId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Default: now. Cannot be in the future.</summary>
    public DateTime? PaymentDateUtc { get; set; }

    /// <summary>Required when the payment method requires a reference.</summary>
    public string? ReferenceNumber { get; set; }

    public string? Notes { get; set; }

    public bool AutoAllocate { get; set; }

    public List<PaymentAllocationRequest> Allocations { get; set; } = new();
}

/// <summary>Body of POST /api/v1/customer-payments/{id}/allocations (allocate the unallocated amount later).</summary>
public sealed class AllocatePaymentRequest : IPaymentAllocationRequest
{
    public bool AutoAllocate { get; set; }

    public List<PaymentAllocationRequest> Allocations { get; set; } = new();
}

/// <summary>
/// GET /api/v1/customer-payments. Search: payment number, reference number, customer code / name.
/// SortBy: paymentDateUtc (default, newest first), paymentNumber, amount, unallocatedAmount.
/// </summary>
public sealed class CustomerPaymentListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "paymentDateUtc", "paymentNumber", "amount", "unallocatedAmount" };

    public long? CustomerId { get; set; }

    public long? BranchId { get; set; }

    public long? PaymentMethodId { get; set; }

    public long? CurrencyId { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    /// <summary>true = only payments with an unallocated amount.</summary>
    public bool? HasUnallocatedAmount { get; set; }
}

public sealed class PaymentAllocationDto
{
    public long Id { get; init; }
    public long InvoiceId { get; init; }
    public string? InvoiceNumber { get; init; }
    public decimal Amount { get; init; }
    public DateTime AllocatedAtUtc { get; init; }
    /// <summary>The invoice balance now.</summary>
    public decimal InvoiceBalanceDue { get; init; }
}

public sealed class CustomerPaymentDto
{
    public long Id { get; init; }
    public string PaymentNumber { get; init; } = string.Empty;
    public long CustomerId { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public long PaymentMethodId { get; init; }
    public string PaymentMethodName { get; init; } = string.Empty;
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime PaymentDateUtc { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? Notes { get; init; }
    public decimal AllocatedAmount { get; init; }
    public decimal UnallocatedAmount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedByUserId { get; init; }
    public IReadOnlyList<PaymentAllocationDto> Allocations { get; init; } = Array.Empty<PaymentAllocationDto>();
}

public sealed class CustomerPaymentListItemDto
{
    public long Id { get; init; }
    public string PaymentNumber { get; init; } = string.Empty;
    public long CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string BranchName { get; init; } = string.Empty;
    public string PaymentMethodName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal AllocatedAmount { get; init; }
    public decimal UnallocatedAmount { get; init; }
    public DateTime PaymentDateUtc { get; init; }
    public string? ReferenceNumber { get; init; }
}

/// <summary>Result of POST /customer-payments: IsReplay = true when the Idempotency-Key was already used.</summary>
public sealed class CustomerPaymentCreationResult
{
    public CustomerPaymentDto Payment { get; init; } = null!;

    public bool IsReplay { get; init; }
}
