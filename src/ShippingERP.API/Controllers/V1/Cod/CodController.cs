using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Controllers.V1.Billing;
using ShippingERP.API.Extensions;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Application.Interfaces.Cod;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Cod;

/// <summary>
/// الدفع عند الاستلام (Cash on delivery): collection, driver handover, review for settlement, disputes, balances,
/// aging and the dashboard. Every action needs COD.View plus its own permission.
///
/// A driver user (Driver role) only sees his own COD shipments and his own balance, and records collections for
/// himself. Branch users see the COD of shipments that start, end or are in their branch.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cod")]
[HasPermission(Permissions.Cod.View)]
public sealed class CodController : ApiControllerBase
{
    private readonly ICodService _cod;
    private readonly ICodReportService _reports;

    public CodController(ICodService cod, ICodReportService reports)
    {
        _cod = cod;
        _reports = reports;
    }

    /// <summary>
    /// COD records. Filters: status, senderCustomerId, driverId, branchId (origin), currencyId, cashboxId,
    /// collectedFromUtc / collectedToUtc, hasRemainingPayable. Search: shipment / tracking number, sender.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CodTransactionListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<CodTransactionListItemDto>>>> GetAll(
        [FromQuery] CodTransactionListQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _cod.GetPagedAsync(query, cancellationToken));
    }

    /// <summary>One COD record with its deductions, settlement state and full history.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<CodTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CodTransactionDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _cod.GetByIdAsync(id, cancellationToken));
    }

    [HttpGet("by-shipment/{shipmentId:long}")]
    [ProducesResponseType(typeof(ApiResponse<CodTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CodTransactionDto>>> GetByShipment(long shipmentId, CancellationToken cancellationToken)
    {
        return OkResponse(await _cod.GetByShipmentAsync(shipmentId, cancellationToken));
    }

    /// <summary>
    /// Records the money collected from the recipient of a DELIVERED COD shipment (by shipmentId or trackingNumber).
    /// Without cashboxId: the money goes to the driver's cash balance. With cashboxId: the recipient paid at the
    /// branch counter (needs COD.Handover). A different amount needs differenceReason and opens a dispute.
    /// 409 when the collection was already recorded.
    /// </summary>
    [HttpPost("collect")]
    [HasPermission(Permissions.Cod.Collect)]
    [ProducesResponseType(typeof(ApiResponse<CodTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CodTransactionDto>>> Collect([FromBody] CollectCodRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _cod.CollectAsync(request, cancellationToken), "تم تسجيل التحصيل");
    }

    /// <summary>
    /// A driver hands cash over to a cashbox: driver handover receipt (RCV-...), cashbox in, driver balance out,
    /// the COD shipments become "received at branch cashbox". The amount cannot exceed the driver's balance.
    /// Send "Idempotency-Key": a retried request returns the first receipt (200 + Idempotent-Replayed: true).
    /// </summary>
    [HttpPost("driver-handover")]
    [HasPermission(Permissions.Cod.Handover)]
    [ProducesResponseType(typeof(ApiResponse<CashReceiptVoucherDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CashReceiptVoucherDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CashReceiptVoucherDto>>> DriverHandover(
        [FromBody] DriverHandoverRequest request,
        [FromHeader(Name = CustomerPaymentsController.IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _cod.HandoverAsync(request, idempotencyKey, cancellationToken);
        if (result.IsReplay)
        {
            Response.Headers[CustomerPaymentsController.ReplayedHeader] = "true";
            return Ok(ApiResponse<CashReceiptVoucherDto>.Ok(result.Voucher, "هذا الاستلام مسجل من قبل بنفس مفتاح Idempotency-Key", HttpContext.GetTraceId()));
        }

        var message = result.Voucher.ShortageAmount > 0m
            ? $"تم الاستلام. يوجد عجز {result.Voucher.ShortageAmount} يبقى على عهدة السائق"
            : "تم استلام نقدية السائق";
        // The receipt lives in the cash receipts resource (GET /api/v1/cash-receipts/{id}, printable at .../pdf).
        return Created(
            $"/api/v1/cash-receipts/{result.Voucher.Id}",
            ApiResponse<CashReceiptVoucherDto>.Ok(result.Voucher, message, HttpContext.GetTraceId()));
    }

    /// <summary>
    /// Reviewed COD shipments received in a cashbox become ready for the sender settlement: the shipping charge,
    /// COD commission, tax and approved fees are deducted and the net amount is fixed (an invoiced shipment's charge
    /// is not deducted). Give codTransactionIds or senderCustomerId.
    /// </summary>
    [HttpPost("mark-ready")]
    [HasPermission(Permissions.Cod.Settle)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CodTransactionListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CodTransactionListItemDto>>>> MarkReady(
        [FromBody] MarkCodReadyRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _cod.MarkReadyAsync(request, cancellationToken), "أصبحت الشحنات جاهزة للتسوية");
    }

    /// <summary>Blocks a collected COD (wrong amount, complaint...) until a supervisor resolves it.</summary>
    [HttpPost("{id:long}/dispute")]
    [HasPermission(Permissions.Cod.Adjust)]
    [ProducesResponseType(typeof(ApiResponse<CodTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CodTransactionDto>>> OpenDispute(
        long id,
        [FromBody] OpenCodDisputeRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _cod.OpenDisputeAsync(id, request, cancellationToken), "تم فتح النزاع");
    }

    /// <summary>Resolves a dispute (another user than the one who opened it): the COD goes back to its previous state.</summary>
    [HttpPost("{id:long}/resolve-dispute")]
    [HasPermission(Permissions.Cod.ApproveAdjustment)]
    [ProducesResponseType(typeof(ApiResponse<CodTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CodTransactionDto>>> ResolveDispute(
        long id,
        [FromBody] ResolveCodDisputeRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _cod.ResolveDisputeAsync(id, request, cancellationToken), "تم حل النزاع");
    }

    /// <summary>أرصدة عهد السائقين: what each driver holds, the COD not handed over yet and the oldest one.</summary>
    [HttpGet("driver-balances")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DriverBalanceDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<DriverBalanceDto>>>> DriverBalances(
        [FromQuery] DriverBalanceQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetDriverBalancesAsync(query, cancellationToken));
    }

    /// <summary>The driver's cash statement (collections, handovers, adjustments) with the running balance.</summary>
    [HttpGet("driver-balances/{driverId:long}/movements")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DriverCashMovementDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PagedResult<DriverCashMovementDto>>>> DriverMovements(
        long driverId,
        [FromQuery] DriverMovementQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetDriverMovementsAsync(driverId, query, cancellationToken));
    }

    /// <summary>أرصدة المرسلين: the sender money held by the company, by stage, and what is ready to pay.</summary>
    [HttpGet("sender-balances")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SenderBalanceDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<SenderBalanceDto>>>> SenderBalances(
        [FromQuery] SenderBalanceQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetSenderBalancesAsync(query, cancellationToken));
    }

    /// <summary>أعمار مبالغ التحصيل: outstanding COD by sender and age (0-7, 8-15, 16-30, 31-60, 60+ days) and by stage.</summary>
    [HttpGet("aging")]
    [ProducesResponseType(typeof(ApiResponse<CodAgingReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CodAgingReportDto>>> Aging([FromQuery] CodAgingQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetAgingAsync(query, cancellationToken));
    }

    /// <summary>لوحة الدفع عند الاستلام: counts and amounts by stage, drivers, cashboxes, open settlements and approvals waiting.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<CodDashboardDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CodDashboardDto>>> Dashboard([FromQuery] CodDashboardQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetDashboardAsync(query, cancellationToken));
    }
}
