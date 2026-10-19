using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Billing;

// ------------------------------------------------------------------ requests

/// <summary>Options shared by both ways of creating an invoice.</summary>
public abstract class CreateInvoiceOptions
{
    /// <summary>Issuing branch. Default: the caller's branch, otherwise the origin branch of the shipments.</summary>
    public long? BranchId { get; set; }

    /// <summary>Default: ShippingSettings.DefaultPaymentTermsDays.</summary>
    public int? PaymentTermsDays { get; set; }

    public string? Notes { get; set; }

    /// <summary>Issue at once (needs Invoices.Issue). Otherwise the invoice stays a Draft.</summary>
    public bool IssueImmediately { get; set; }
}

/// <summary>Body of POST /api/v1/invoices/from-shipment/{shipmentId} (bill-to: the sender).</summary>
public sealed class CreateInvoiceFromShipmentRequest : CreateInvoiceOptions
{
}

/// <summary>
/// Body of POST /api/v1/invoices/consolidated: one invoice for several shipments of the same customer (sender)
/// and currency. Give ShipmentIds, or leave it empty to take every eligible shipment created in [FromUtc, ToUtc).
/// </summary>
public sealed class CreateConsolidatedInvoiceRequest : CreateInvoiceOptions
{
    public long CustomerId { get; set; }

    /// <summary>Up to 500 shipments. Empty = all eligible shipments of the period.</summary>
    public List<long> ShipmentIds { get; set; } = new();

    /// <summary>Required with an empty ShipmentIds (with ToUtc).</summary>
    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    /// <summary>Default: the base currency (only used with the period).</summary>
    public long? CurrencyId { get; set; }
}

/// <summary>Body of PUT /api/v1/invoices/{id} (drafts only).</summary>
public sealed class UpdateDraftInvoiceRequest
{
    public int PaymentTermsDays { get; set; }

    /// <summary>Discount on the whole invoice, spread over the lines in proportion to their amounts.</summary>
    public decimal InvoiceDiscountAmount { get; set; }

    public string? Notes { get; set; }
}

/// <summary>Body of POST /api/v1/invoices/{id}/issue.</summary>
public sealed class IssueInvoiceRequest
{
    /// <summary>Default: now. Cannot be in the future. The due date = invoice date + payment terms.</summary>
    public DateTime? InvoiceDateUtc { get; set; }
}

public sealed class CancelInvoiceRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class CreditNoteLineRequest
{
    public long InvoiceLineId { get; set; }

    /// <summary>Amount before tax to credit. Empty = everything not yet credited on this line.</summary>
    public decimal? TaxableAmount { get; set; }
}

/// <summary>
/// Body of POST /api/v1/invoices/{id}/credit-notes. Either CreditAll = true (every line, everything not yet
/// credited) or a list of lines. The tax is reversed at each line's tax rate.
/// </summary>
public sealed class CreateCreditNoteRequest
{
    public string Reason { get; set; } = string.Empty;

    public bool CreditAll { get; set; }

    public List<CreditNoteLineRequest> Lines { get; set; } = new();
}

/// <summary>
/// GET /api/v1/invoices (and /export). Search: invoice number, customer code / name / phone.
/// SortBy: createdAtUtc (default, newest first), invoiceDateUtc, invoiceNumber, dueDateUtc, totalAmount, balanceDue, status.
/// </summary>
public sealed class InvoiceListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields =
        { "createdAtUtc", "invoiceDateUtc", "invoiceNumber", "dueDateUtc", "totalAmount", "balanceDue", "status" };

    public InvoiceStatus? Status { get; set; }

    public long? CustomerId { get; set; }

    public long? BranchId { get; set; }

    public long? CurrencyId { get; set; }

    /// <summary>Invoices that bill this shipment.</summary>
    public long? ShipmentId { get; set; }

    /// <summary>Invoice date on or after (drafts: creation date).</summary>
    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    /// <summary>true = only invoices with a balance.</summary>
    public bool? HasBalance { get; set; }
}

/// <summary>GET /api/v1/invoices/eligible-shipments: shipments of a customer that can still be invoiced.</summary>
public sealed class EligibleShipmentsQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "createdAtUtc", "shipmentNumber", "totalCharge" };

    public long CustomerId { get; set; }

    public long? CurrencyId { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }
}

// ------------------------------------------------------------------ responses

public sealed class InvoiceListItemDto
{
    public long Id { get; init; }
    public string? InvoiceNumber { get; init; }
    public InvoiceStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public long CustomerId { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public bool IsConsolidated { get; init; }
    public int LineCount { get; init; }
    public DateTime? InvoiceDateUtc { get; init; }
    public DateTime? DueDateUtc { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal CreditedAmount { get; init; }
    public decimal BalanceDue { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class InvoiceLineDto
{
    public long Id { get; init; }
    public int LineNumber { get; init; }
    public long ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public long PricingSnapshotId { get; init; }
    public InvoiceChargeType ChargeType { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal ShippingCharge { get; init; }
    public decimal LineDiscountAmount { get; init; }
    public decimal InvoiceDiscountAmount { get; init; }
    public decimal TaxableAmount { get; init; }
    public decimal TaxPercentage { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal LineTotal { get; init; }
    public decimal CreditedTaxableAmount { get; init; }
    public decimal CreditedTaxAmount { get; init; }
    /// <summary>Taxable amount that can still be credited.</summary>
    public decimal CreditableAmount { get; init; }
}

public sealed class InvoicePaymentDto
{
    public long PaymentId { get; init; }
    public string PaymentNumber { get; init; } = string.Empty;
    public string PaymentMethodName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime AllocatedAtUtc { get; init; }
}

public sealed class CreditNoteLineDto
{
    public long Id { get; init; }
    public long InvoiceLineId { get; init; }
    public long ShipmentId { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal TaxableAmount { get; init; }
    public decimal TaxPercentage { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal LineTotal { get; init; }
}

public sealed class CreditNoteDto
{
    public long Id { get; init; }
    public string CreditNoteNumber { get; init; } = string.Empty;
    public long InvoiceId { get; init; }
    public string? InvoiceNumber { get; init; }
    public long CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTime CreditNoteDateUtc { get; init; }
    public string Reason { get; init; } = string.Empty;
    public decimal TaxableAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal AppliedToInvoiceAmount { get; init; }
    public decimal UnappliedAmount { get; init; }
    public long? CreatedByUserId { get; init; }
    public IReadOnlyList<CreditNoteLineDto> Lines { get; init; } = Array.Empty<CreditNoteLineDto>();
}

public sealed class InvoiceDto
{
    public long Id { get; init; }
    public string? InvoiceNumber { get; init; }
    public InvoiceStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public IReadOnlyList<InvoiceStatus> NextStatuses { get; init; } = Array.Empty<InvoiceStatus>();
    public bool IsConsolidated { get; init; }

    public long CustomerId { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public string? CustomerTaxNumber { get; init; }

    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public int CurrencyDecimals { get; init; }

    public DateTime? InvoiceDateUtc { get; init; }
    public int PaymentTermsDays { get; init; }
    public DateTime? DueDateUtc { get; init; }

    public decimal SubTotal { get; init; }
    public decimal LineDiscountAmount { get; init; }
    public decimal InvoiceDiscountAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxableAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal CreditedAmount { get; init; }
    public decimal CreditAppliedAmount { get; init; }
    public decimal BalanceDue { get; init; }

    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedByUserId { get; init; }
    public DateTime? IssuedAtUtc { get; init; }
    public long? IssuedByUserId { get; init; }
    public DateTime? PaidAtUtc { get; init; }
    public DateTime? CancelledAtUtc { get; init; }
    public string? CancellationReason { get; init; }

    public IReadOnlyList<InvoiceLineDto> Lines { get; init; } = Array.Empty<InvoiceLineDto>();
    public IReadOnlyList<InvoicePaymentDto> Payments { get; init; } = Array.Empty<InvoicePaymentDto>();
    public IReadOnlyList<CreditNoteDto> CreditNotes { get; init; } = Array.Empty<CreditNoteDto>();
}

public sealed class EligibleShipmentDto
{
    public long ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public string StatusName { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;
    public string DestinationCityName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal ShippingCharge { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalCharge { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
