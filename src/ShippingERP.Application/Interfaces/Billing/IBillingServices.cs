using ShippingERP.Application.Common.Files;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Billing;

namespace ShippingERP.Application.Interfaces.Billing;

// Customer invoicing and receivables (step 5).
// Branch rule: users without Branches.ViewAll only see and create documents of their own branch; a document of
// another branch is reported as 404. Every money change runs in ONE database transaction together with its
// customer sub-ledger rows, its audit log and its outbox event.

public interface IInvoiceService
{
    Task<PagedResult<InvoiceListItemDto>> GetPagedAsync(InvoiceListQuery query, CancellationToken cancellationToken);

    Task<InvoiceDto> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<PagedResult<EligibleShipmentDto>> GetEligibleShipmentsAsync(EligibleShipmentsQuery query, CancellationToken cancellationToken);

    /// <summary>Invoice for one shipment (bill-to = the sender). 409 when the shipment is already invoiced.</summary>
    Task<InvoiceDto> CreateFromShipmentAsync(long shipmentId, CreateInvoiceFromShipmentRequest request, CancellationToken cancellationToken);

    /// <summary>One invoice for several shipments of the same customer and currency.</summary>
    Task<InvoiceDto> CreateConsolidatedAsync(CreateConsolidatedInvoiceRequest request, CancellationToken cancellationToken);

    /// <summary>Drafts only: terms, invoice discount, notes.</summary>
    Task<InvoiceDto> UpdateDraftAsync(long id, UpdateDraftInvoiceRequest request, CancellationToken cancellationToken);

    Task<InvoiceDto> IssueAsync(long id, IssueInvoiceRequest request, CancellationToken cancellationToken);

    Task<InvoiceDto> CancelAsync(long id, CancelInvoiceRequest request, CancellationToken cancellationToken);

    Task<CreditNoteDto> CreateCreditNoteAsync(long invoiceId, CreateCreditNoteRequest request, CancellationToken cancellationToken);

    Task<CreditNoteDto> GetCreditNoteAsync(long creditNoteId, CancellationToken cancellationToken);

    /// <summary>Printable A4 invoice (Arabic, right-to-left). Drafts are watermarked.</summary>
    Task<FileDownload> GetPdfAsync(long id, CancellationToken cancellationToken);

    /// <summary>The filtered list as an Excel file (no paging, up to 10,000 rows).</summary>
    Task<FileDownload> ExportAsync(InvoiceListQuery query, CancellationToken cancellationToken);

    /// <summary>Marks open invoices past their due date as Overdue (run by a background job). Returns the count.</summary>
    Task<int> RefreshOverdueAsync(CancellationToken cancellationToken);
}

public interface ICustomerPaymentService
{
    Task<PagedResult<CustomerPaymentListItemDto>> GetPagedAsync(CustomerPaymentListQuery query, CancellationToken cancellationToken);

    Task<CustomerPaymentDto> GetByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Records a payment and its allocations in one transaction. With an idempotency key already used by the same
    /// request, returns the existing payment (IsReplay = true); with a different request, 409.
    /// </summary>
    Task<CustomerPaymentCreationResult> CreateAsync(CreateCustomerPaymentRequest request, string? idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Allocates (part of) the unallocated amount of an existing payment.</summary>
    Task<CustomerPaymentDto> AllocateAsync(long paymentId, AllocatePaymentRequest request, CancellationToken cancellationToken);
}

public interface IReceivablesReportService
{
    Task<CustomerStatementDto> GetStatementAsync(long customerId, CustomerStatementQuery query, CancellationToken cancellationToken);

    /// <summary>The whole statement period as an Excel file.</summary>
    Task<FileDownload> ExportStatementAsync(long customerId, CustomerStatementQuery query, CancellationToken cancellationToken);

    Task<CustomerAgingReportDto> GetCustomerAgingAsync(CustomerAgingQuery query, CancellationToken cancellationToken);

    /// <summary>Step 9: current balance of the customer in every currency it has movements in.</summary>
    Task<IReadOnlyList<CustomerBalanceDto>> GetBalancesAsync(long customerId, CancellationToken cancellationToken);
}
