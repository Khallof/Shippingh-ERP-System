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
/// سندات الصرف النقدية (cash payment vouchers): expenses, supplier payments, employee advances, customer refunds.
/// The sender settlement vouchers are created by POST /cod/settlements/{id}/pay. The cashbox must hold the amount.
/// POST accepts "Idempotency-Key".
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cash-payments")]
[HasPermission(Permissions.Cashboxes.View)]
public sealed class CashPaymentsController : ApiControllerBase
{
    private readonly ICashVoucherService _vouchers;

    public CashPaymentsController(ICashVoucherService vouchers)
    {
        _vouchers = vouchers;
    }

    /// <summary>Filters: cashboxId, branchId, status, type (SenderSettlement, Expense, SupplierPayment, EmployeeAdvance, CustomerRefund, Other), fromUtc / toUtc.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CashPaymentVoucherDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<CashPaymentVoucherDto>>>> GetAll([FromQuery] CashVoucherListQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _vouchers.GetPaymentsAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<CashPaymentVoucherDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CashPaymentVoucherDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _vouchers.GetPaymentAsync(id, cancellationToken));
    }

    [HttpPost]
    [HasPermission(Permissions.CashVouchers.Create)]
    [ProducesResponseType(typeof(ApiResponse<CashPaymentVoucherDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CashPaymentVoucherDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CashPaymentVoucherDto>>> Create(
        [FromBody] CreateCashPaymentRequest request,
        [FromHeader(Name = CustomerPaymentsController.IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _vouchers.CreatePaymentAsync(request, idempotencyKey, cancellationToken);
        if (result.IsReplay)
        {
            Response.Headers[CustomerPaymentsController.ReplayedHeader] = "true";
            return Ok(ApiResponse<CashPaymentVoucherDto>.Ok(result.Voucher, "هذا السند مسجل من قبل بنفس مفتاح Idempotency-Key", HttpContext.GetTraceId()));
        }

        return CreatedResponse(nameof(GetById), new { id = result.Voucher.Id, version = "1" }, result.Voucher, "تم تسجيل سند الصرف");
    }

    /// <summary>Cancels a payment voucher (the money returns to the cashbox). Settlement vouchers: reverse the settlement.</summary>
    [HttpPost("{id:long}/cancel")]
    [HasPermission(Permissions.CashVouchers.Cancel)]
    [ProducesResponseType(typeof(ApiResponse<CashPaymentVoucherDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CashPaymentVoucherDto>>> Cancel(
        long id,
        [FromBody] CancelCashVoucherRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _vouchers.CancelPaymentAsync(id, request, cancellationToken), "تم إلغاء سند الصرف");
    }

    /// <summary>Printable payment voucher (A5, Arabic, amount in words, signatures).</summary>
    [HttpGet("{id:long}/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPdf(long id, CancellationToken cancellationToken)
    {
        return FileDownloadResult(await _vouchers.GetPaymentPdfAsync(id, cancellationToken));
    }
}
