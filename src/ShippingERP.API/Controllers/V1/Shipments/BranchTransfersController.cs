using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Shipments;

/// <summary>
/// التحويلات بين الفروع (branch transfer manifests).
/// The sending branch creates, fills and dispatches (Transfers.Manage); the receiving branch receives (Transfers.Receive).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/branch-transfers")]
[HasPermission(Permissions.Transfers.View)]
public sealed class BranchTransfersController : ApiControllerBase
{
    private readonly IBranchTransferService _service;

    public BranchTransfersController(IBranchTransferService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BranchTransferDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<BranchTransferDto>>>> GetAll(
        [FromQuery] BranchTransferListQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetPagedAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<BranchTransferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BranchTransferDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [HasPermission(Permissions.Transfers.Manage)]
    [ProducesResponseType(typeof(ApiResponse<BranchTransferDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<BranchTransferDto>>> Create(
        [FromBody] CreateBranchTransferRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = created.Id, version = "1" }, created, "تم إنشاء التحويل");
    }

    [HttpPost("{id:long}/items")]
    [HasPermission(Permissions.Transfers.Manage)]
    [ProducesResponseType(typeof(ApiResponse<BranchTransferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<BranchTransferDto>>> AddShipments(
        long id,
        [FromBody] ShipmentCodesRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.AddShipmentsAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:long}/items/{shipmentId:long}")]
    [HasPermission(Permissions.Transfers.Manage)]
    [ProducesResponseType(typeof(ApiResponse<BranchTransferDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BranchTransferDto>>> RemoveShipment(long id, long shipmentId, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.RemoveShipmentAsync(id, shipmentId, cancellationToken));
    }

    /// <summary>Sends the transfer: every shipment becomes InTransit.</summary>
    [HttpPost("{id:long}/dispatch")]
    [HasPermission(Permissions.Transfers.Manage)]
    [ProducesResponseType(typeof(ApiResponse<BranchTransferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<BranchTransferDto>>> Dispatch(
        long id,
        [FromBody] DispatchBranchTransferRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.DispatchAsync(id, request, cancellationToken), "تم إرسال التحويل");
    }

    /// <summary>Receives the transfer at the destination branch (receivedCodes empty = everything arrived).</summary>
    [HttpPost("{id:long}/receive")]
    [HasPermission(Permissions.Transfers.Receive)]
    [ProducesResponseType(typeof(ApiResponse<BranchTransferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<BranchTransferDto>>> Receive(
        long id,
        [FromBody] ReceiveBranchTransferRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.ReceiveAsync(id, request, cancellationToken), "تم استلام التحويل");
    }

    [HttpPost("{id:long}/cancel")]
    [HasPermission(Permissions.Transfers.Manage)]
    [ProducesResponseType(typeof(ApiResponse<BranchTransferDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BranchTransferDto>>> Cancel(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.CancelAsync(id, cancellationToken), "تم إلغاء التحويل");
    }
}
