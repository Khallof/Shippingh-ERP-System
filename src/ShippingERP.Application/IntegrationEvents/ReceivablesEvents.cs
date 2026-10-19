namespace ShippingERP.Application.IntegrationEvents;

// Receivables events (step 5) for the accounting module. Amounts are in the document currency.

public sealed record InvoiceTaxLine(long? TaxRateId, decimal TaxPercentage, decimal TaxableAmount, decimal TaxAmount);

public sealed record InvoiceIssuedEvent : IntegrationEvent
{
    public override string EventType => "InvoiceIssued";

    public long InvoiceId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public long CustomerId { get; init; }
    public long BranchId { get; init; }
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTime InvoiceDateUtc { get; init; }
    public DateTime DueDateUtc { get; init; }
    public decimal SubTotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxableAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }

    /// <summary>Taxable amount and tax grouped by tax rate (for the tax accounts).</summary>
    public IReadOnlyList<InvoiceTaxLine> TaxLines { get; init; } = Array.Empty<InvoiceTaxLine>();

    public IReadOnlyList<long> ShipmentIds { get; init; } = Array.Empty<long>();
}

public sealed record InvoiceCancelledEvent : IntegrationEvent
{
    public override string EventType => "InvoiceCancelled";

    public long InvoiceId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public long CustomerId { get; init; }
    public long BranchId { get; init; }
    public long CurrencyId { get; init; }
    public decimal TaxableAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed record CreditNoteIssuedEvent : IntegrationEvent
{
    public override string EventType => "CreditNoteIssued";

    public long CreditNoteId { get; init; }
    public string CreditNoteNumber { get; init; } = string.Empty;
    public long InvoiceId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public long CustomerId { get; init; }
    public long BranchId { get; init; }
    public long CurrencyId { get; init; }
    public decimal TaxableAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal AppliedToInvoiceAmount { get; init; }
    public decimal UnappliedAmount { get; init; }
    public IReadOnlyList<InvoiceTaxLine> TaxLines { get; init; } = Array.Empty<InvoiceTaxLine>();
}

public sealed record PaymentAllocationLine(long InvoiceId, string? InvoiceNumber, decimal Amount);

public sealed record CustomerPaymentReceivedEvent : IntegrationEvent
{
    public override string EventType => "CustomerPaymentReceived";

    public long PaymentId { get; init; }
    public string PaymentNumber { get; init; } = string.Empty;
    public long CustomerId { get; init; }
    public long BranchId { get; init; }
    public long PaymentMethodId { get; init; }
    public string PaymentMethodKind { get; init; } = string.Empty;
    public long CurrencyId { get; init; }
    public decimal Amount { get; init; }
    public DateTime PaymentDateUtc { get; init; }
    public string? ReferenceNumber { get; init; }
    public IReadOnlyList<PaymentAllocationLine> Allocations { get; init; } = Array.Empty<PaymentAllocationLine>();
}

/// <summary>Allocation of an existing payment made later (no new money; receivable sub-accounts only).</summary>
public sealed record CustomerPaymentAllocatedEvent : IntegrationEvent
{
    public override string EventType => "CustomerPaymentAllocated";

    public long PaymentId { get; init; }
    public string PaymentNumber { get; init; } = string.Empty;
    public long CustomerId { get; init; }
    public long CurrencyId { get; init; }
    public IReadOnlyList<PaymentAllocationLine> Allocations { get; init; } = Array.Empty<PaymentAllocationLine>();
}
