using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Application.DTOs.Reports;
using ShippingERP.Application.Interfaces.Billing;
using ShippingERP.Application.Interfaces.Reports;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Billing;

/// <summary>
/// الفواتير والإشعارات الدائنة (Invoices and credit notes).
///
/// Life cycle: create (Draft) -> issue (number INV-yyyy-nnnnnn, customer account debited) -> payments / credit notes.
/// An issued invoice is never edited: use a credit note, or cancel it while nothing is paid or credited.
/// A shipment charge can be on only ONE active invoice (409 when it is already invoiced).
/// Branch users only see the invoices of their branch.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/invoices")]
[HasPermission(Permissions.Invoices.View)]
public sealed class InvoicesController : ApiControllerBase
{
    private readonly IInvoiceService _invoices;

    public InvoicesController(IInvoiceService invoices, IReportExportService exports)
    {
        _invoices = invoices;
        _exports = exports;
    }

    private readonly IReportExportService _exports;

    /// <summary>Search / filter / sort / page (search: invoice number, customer code / name / phone).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<InvoiceListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<InvoiceListItemDto>>>> GetAll(
        [FromQuery] InvoiceListQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _invoices.GetPagedAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _invoices.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>Shipments of a customer (as sender) that can still be invoiced.</summary>
    [HttpGet("eligible-shipments")]
    [HasPermission(Permissions.Invoices.Create)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<EligibleShipmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<EligibleShipmentDto>>>> GetEligibleShipments(
        [FromQuery] EligibleShipmentsQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _invoices.GetEligibleShipmentsAsync(query, cancellationToken));
    }

    /// <summary>Invoice for one shipment; the customer is the sender.</summary>
    [HttpPost("from-shipment/{shipmentId:long}")]
    [HasPermission(Permissions.Invoices.Create)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> CreateFromShipment(
        long shipmentId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateInvoiceFromShipmentRequest? request,
        CancellationToken cancellationToken)
    {
        var created = await _invoices.CreateFromShipmentAsync(shipmentId, request ?? new CreateInvoiceFromShipmentRequest(), cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = created.Id, version = "1" }, created, "تم إنشاء الفاتورة");
    }

    /// <summary>One invoice for several shipments of the same customer and currency (list or period).</summary>
    [HttpPost("consolidated")]
    [HasPermission(Permissions.Invoices.Create)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> CreateConsolidated(
        [FromBody] CreateConsolidatedInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _invoices.CreateConsolidatedAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = created.Id, version = "1" }, created, "تم إنشاء الفاتورة المجمعة");
    }

    /// <summary>Drafts only: payment terms, invoice discount, notes.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Invoices.Create)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> UpdateDraft(
        long id,
        [FromBody] UpdateDraftInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _invoices.UpdateDraftAsync(id, request, cancellationToken), "تم حفظ المسودة");
    }

    /// <summary>Gives the number and the due date, and debits the customer account. Body optional.</summary>
    [HttpPost("{id:long}/issue")]
    [HasPermission(Permissions.Invoices.Issue)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> Issue(
        long id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] IssueInvoiceRequest? request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _invoices.IssueAsync(id, request ?? new IssueInvoiceRequest(), cancellationToken), "تم إصدار الفاتورة");
    }

    /// <summary>Cancels a draft, or an issued invoice without payments and credit notes (frees its shipments).</summary>
    [HttpPost("{id:long}/cancel")]
    [HasPermission(Permissions.Invoices.Cancel)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> Cancel(
        long id,
        [FromBody] CancelInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _invoices.CancelAsync(id, request, cancellationToken), "تم إلغاء الفاتورة");
    }

    /// <summary>Credit note (CRN-yyyy-nnnnnn) for some lines or the whole invoice.</summary>
    [HttpPost("{id:long}/credit-notes")]
    [HasPermission(Permissions.Invoices.CreditNotes)]
    [ProducesResponseType(typeof(ApiResponse<CreditNoteDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CreditNoteDto>>> CreateCreditNote(
        long id,
        [FromBody] CreateCreditNoteRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _invoices.CreateCreditNoteAsync(id, request, cancellationToken);
        return CreatedResponse(nameof(GetCreditNote), new { creditNoteId = created.Id, version = "1" }, created, "تم إصدار الإشعار الدائن");
    }

    [HttpGet("credit-notes/{creditNoteId:long}")]
    [ProducesResponseType(typeof(ApiResponse<CreditNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CreditNoteDto>>> GetCreditNote(long creditNoteId, CancellationToken cancellationToken)
    {
        return OkResponse(await _invoices.GetCreditNoteAsync(creditNoteId, cancellationToken));
    }

    /// <summary>Printable A4 PDF (Arabic, right-to-left). Drafts are marked as drafts.</summary>
    [HttpGet("{id:long}/pdf")]
    [HasPermission(Permissions.Invoices.Export)]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPdf(long id, CancellationToken cancellationToken)
    {
        return FileDownloadResult(await _invoices.GetPdfAsync(id, cancellationToken));
    }

    /// <summary>
    /// The filtered list (same filters as GET /invoices, no paging, up to 10,000 rows): ?format=excel (default) or
    /// ?format=pdf (step 9, with the company header and logo).
    /// </summary>
    [HttpGet("export")]
    [HasPermission(Permissions.Invoices.Export)]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/pdf", "application/json")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Export([FromQuery] InvoiceListQuery query, [FromQuery] ExportQuery export, CancellationToken cancellationToken)
    {
        return FileDownloadResult(export.Format == ExportFormat.Pdf
            ? await _exports.InvoicesAsync(query, ExportFormat.Pdf, cancellationToken)
            : await _invoices.ExportAsync(query, cancellationToken));
    }
}
