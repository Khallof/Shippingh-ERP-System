using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Cash;

/// <summary>
/// الصناديق (cashboxes): several per branch and currency, their statement (transactions with the running balance)
/// and the daily closing with discrepancy approval. Every action needs Cashboxes.View; branch users only see
/// the cashboxes of their branch.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cashboxes")]
[HasPermission(Permissions.Cashboxes.View)]
public sealed class CashboxesController : ApiControllerBase
{
    private readonly ICashboxService _cashboxes;

    public CashboxesController(ICashboxService cashboxes)
    {
        _cashboxes = cashboxes;
    }

    /// <summary>Cashboxes with their current balance. Filters: branchId, currencyId, isActive. Search: code / name.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CashboxDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<CashboxDto>>>> GetAll([FromQuery] CashboxListQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _cashboxes.GetPagedAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<CashboxDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CashboxDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _cashboxes.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>Creates a cashbox; an opening balance is posted as its first transaction.</summary>
    [HttpPost]
    [HasPermission(Permissions.Cashboxes.Manage)]
    [ProducesResponseType(typeof(ApiResponse<CashboxDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CashboxDto>>> Create([FromBody] CreateCashboxRequest request, CancellationToken cancellationToken)
    {
        var cashbox = await _cashboxes.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = cashbox.Id, version = "1" }, cashbox, "تم إنشاء الصندوق");
    }

    /// <summary>Name, cashier, notes, active flag (deactivation needs a zero balance). Branch and currency never change.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Cashboxes.Manage)]
    [ProducesResponseType(typeof(ApiResponse<CashboxDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CashboxDto>>> Update(long id, [FromBody] UpdateCashboxRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _cashboxes.UpdateAsync(id, request, cancellationToken), "تم حفظ الصندوق");
    }

    /// <summary>كشف حركة الصندوق: newest first, with the balance after each transaction. Dates are local business dates.</summary>
    [HttpGet("{id:long}/transactions")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CashboxTransactionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PagedResult<CashboxTransactionDto>>>> Transactions(
        long id,
        [FromQuery] CashboxTransactionQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _cashboxes.GetTransactionsAsync(id, query, cancellationToken));
    }

    /// <summary>
    /// Daily closing: counted cash vs. expected balance. A difference needs discrepancyReason and the approval of
    /// another user (POST daily-closings/{id}/approve). Once closed, the day accepts no other movement.
    /// </summary>
    [HttpPost("{id:long}/daily-closings")]
    [HasPermission(Permissions.Cashboxes.Close)]
    [ProducesResponseType(typeof(ApiResponse<CashboxDailyClosingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CashboxDailyClosingDto>>> CloseDay(
        long id,
        [FromBody] CloseCashboxDayRequest request,
        CancellationToken cancellationToken)
    {
        var closing = await _cashboxes.CloseDayAsync(id, request, cancellationToken);
        return OkResponse(closing, closing.Discrepancy == 0m ? "تم إقفال اليوم" : "تم تسجيل الإقفال، والفرق بانتظار الموافقة");
    }

    [HttpGet("{id:long}/daily-closings")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CashboxDailyClosingDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<CashboxDailyClosingDto>>>> ClosingsOfCashbox(
        long id,
        [FromQuery] CashboxClosingQuery query,
        CancellationToken cancellationToken)
    {
        query.CashboxId = id;
        return OkResponse(await _cashboxes.GetClosingsAsync(query, cancellationToken));
    }

    /// <summary>Daily closings of all visible cashboxes. hasDiscrepancy=true gives the cash discrepancy report.</summary>
    [HttpGet("daily-closings")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CashboxDailyClosingDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<CashboxDailyClosingDto>>>> Closings(
        [FromQuery] CashboxClosingQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _cashboxes.GetClosingsAsync(query, cancellationToken));
    }

    [HttpPost("daily-closings/{closingId:long}/approve")]
    [HasPermission(Permissions.Cashboxes.ApproveClosing)]
    [ProducesResponseType(typeof(ApiResponse<CashboxDailyClosingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CashboxDailyClosingDto>>> ApproveClosing(
        long closingId,
        [FromBody] DecideCashboxClosingRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _cashboxes.ApproveClosingAsync(closingId, request, cancellationToken), "تم اعتماد فرق الإقفال وترحيله");
    }

    [HttpPost("daily-closings/{closingId:long}/reject")]
    [HasPermission(Permissions.Cashboxes.ApproveClosing)]
    [ProducesResponseType(typeof(ApiResponse<CashboxDailyClosingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CashboxDailyClosingDto>>> RejectClosing(
        long closingId,
        [FromBody] DecideCashboxClosingRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _cashboxes.RejectClosingAsync(closingId, request, cancellationToken), "تم رفض الإقفال؛ أعد العد");
    }
}
