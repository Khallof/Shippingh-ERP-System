using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Controllers.V1.Billing;
using ShippingERP.API.Extensions;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Cash;

/// <summary>
/// سندات القبض النقدية (cash receipt vouchers), including the driver handover receipts and the counter COD
/// collections made by the COD endpoints. POST accepts "Idempotency-Key" (same rules as customer payments).
/// A posted voucher is never edited: it is cancelled with a reason (CashVouchers.Cancel).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cash-receipts")]
[HasPermission(Permissions.Cashboxes.View)]
public sealed class CashReceiptsController : ApiControllerBase
{
    private readonly ICashVoucherService _vouchers;

    public CashReceiptsController(ICashVoucherService vouchers)
    {
        _vouchers = vouchers;
    }

    /// <summary>Filters: cashboxId, branchId, status, type (DriverHandover, CounterCodCollection, CustomerPayment, OtherIncome, Other), driverId, fromUtc / toUtc.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CashReceiptVoucherDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<CashReceiptVoucherDto>>>> GetAll([FromQuery] CashVoucherListQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _vouchers.GetReceiptsAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<CashReceiptVoucherDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CashReceiptVoucherDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _vouchers.GetReceiptAsync(id, cancellationToken));
    }

    [HttpPost]
    [HasPermission(Permissions.CashVouchers.Create)]
    [ProducesResponseType(typeof(ApiResponse<CashReceiptVoucherDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CashReceiptVoucherDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CashReceiptVoucherDto>>> Create(
        [FromBody] CreateCashReceiptRequest request,
        [FromHeader(Name = CustomerPaymentsController.IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _vouchers.CreateReceiptAsync(request, idempotencyKey, cancellationToken);
        if (result.IsReplay)
        {
            Response.Headers[CustomerPaymentsController.ReplayedHeader] = "true";
            return Ok(ApiResponse<CashReceiptVoucherDto>.Ok(result.Voucher, "هذا السند مسجل من قبل بنفس مفتاح Idempotency-Key", HttpContext.GetTraceId()));
        }

        return CreatedResponse(nameof(GetById), new { id = result.Voucher.Id, version = "1" }, result.Voucher, "تم تسجيل سند القبض");
    }

    /// <summary>
    /// Cancels a receipt (reversing cashbox transaction; the cashbox must still hold the amount). A driver handover
    /// goes back to the driver's balance; a counter COD collection goes back to pending.
    /// </summary>
    [HttpPost("{id:long}/cancel")]
    [HasPermission(Permissions.CashVouchers.Cancel)]
    [ProducesResponseType(typeof(ApiResponse<CashReceiptVoucherDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CashReceiptVoucherDto>>> Cancel(
        long id,
        [FromBody] CancelCashVoucherRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _vouchers.CancelReceiptAsync(id, request, cancellationToken), "تم إلغاء سند القبض");
    }

    /// <summary>Printable receipt (A5, Arabic, amount in words). A driver handover prints the list of COD shipments.</summary>
    [HttpGet("{id:long}/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPdf(long id, CancellationToken cancellationToken)
    {
        return FileDownloadResult(await _vouchers.GetReceiptPdfAsync(id, cancellationToken));
    }
}
