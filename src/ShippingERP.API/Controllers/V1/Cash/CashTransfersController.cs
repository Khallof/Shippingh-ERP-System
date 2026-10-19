using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Cash;

/// <summary>
/// تحويلات النقدية (cash transfers): between cashboxes of a branch, to another branch, or to the bank.
/// Requested (CashTransfers.Create) -> approved by another user (CashTransfers.Approve; the money leaves the source)
/// -> received by the destination branch (CashTransfers.Receive) when it is another branch.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cash-transfers")]
[HasPermission(Permissions.Cashboxes.View)]
public sealed class CashTransfersController : ApiControllerBase
{
    private readonly ICashTransferService _transfers;

    public CashTransfersController(ICashTransferService transfers)
    {
        _transfers = transfers;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CashTransferDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<CashTransferDto>>>> GetAll([FromQuery] CashTransferListQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _transfers.GetPagedAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<CashTransferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CashTransferDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _transfers.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [HasPermission(Permissions.CashTransfers.Create)]
    [ProducesResponseType(typeof(ApiResponse<CashTransferDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CashTransferDto>>> Create([FromBody] CreateCashTransferRequest request, CancellationToken cancellationToken)
    {
        var transfer = await _transfers.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = transfer.Id, version = "1" }, transfer, "تم تسجيل طلب التحويل بانتظار الموافقة");
    }

    [HttpPost("{id:long}/approve")]
    [HasPermission(Permissions.CashTransfers.Approve)]
    [ProducesResponseType(typeof(ApiResponse<CashTransferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CashTransferDto>>> Approve(long id, [FromBody] ApproveCashTransferRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _transfers.ApproveAsync(id, request, cancellationToken), "تم اعتماد التحويل");
    }

    [HttpPost("{id:long}/reject")]
    [HasPermission(Permissions.CashTransfers.Approve)]
    [ProducesResponseType(typeof(ApiResponse<CashTransferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CashTransferDto>>> Reject(long id, [FromBody] CashTransferReasonRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _transfers.RejectAsync(id, request, cancellationToken), "تم رفض التحويل");
    }

    [HttpPost("{id:long}/cancel")]
    [HasPermission(Permissions.CashTransfers.Create)]
    [ProducesResponseType(typeof(ApiResponse<CashTransferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CashTransferDto>>> Cancel(long id, [FromBody] CashTransferReasonRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _transfers.CancelAsync(id, request, cancellationToken), "تم إلغاء التحويل");
    }

    /// <summary>The destination branch confirms receipt of an inter-branch transfer (the money enters its cashbox).</summary>
    [HttpPost("{id:long}/receive")]
    [HasPermission(Permissions.CashTransfers.Receive)]
    [ProducesResponseType(typeof(ApiResponse<CashTransferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CashTransferDto>>> Receive(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _transfers.ReceiveAsync(id, cancellationToken), "تم استلام التحويل");
    }
}
