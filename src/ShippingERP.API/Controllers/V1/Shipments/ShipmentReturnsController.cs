using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Shipments;

/// <summary>
/// المرتجعات (return to sender). Reading requires Shipments.View; changes require Returns.Manage.
/// Open -> (AfterDelivery: receive) -> move to the return branch with a branch transfer -> complete (Returned).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shipment-returns")]
[HasPermission(Permissions.Shipments.View)]
public sealed class ShipmentReturnsController : ApiControllerBase
{
    private readonly IShipmentReturnService _service;

    public ShipmentReturnsController(IShipmentReturnService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ShipmentReturnDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ShipmentReturnDto>>>> GetAll(
        [FromQuery] ShipmentReturnListQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetPagedAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ShipmentReturnDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ShipmentReturnDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [HasPermission(Permissions.Returns.Manage)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentReturnDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ShipmentReturnDto>>> Create(
        [FromBody] CreateShipmentReturnRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = created.Id, version = "1" }, created, "تم فتح المرتجع");
    }

    /// <summary>AfterDelivery returns: the delivered shipment came back to a branch.</summary>
    [HttpPost("{id:long}/receive")]
    [HasPermission(Permissions.Returns.Manage)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentReturnDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ShipmentReturnDto>>> Receive(
        long id,
        [FromBody] ReceiveShipmentReturnRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.ReceiveBackAsync(id, request, cancellationToken), "تم استلام المرتجع");
    }

    /// <summary>The sender got the shipment back (status Returned).</summary>
    [HttpPost("{id:long}/complete")]
    [HasPermission(Permissions.Returns.Manage)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentReturnDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ShipmentReturnDto>>> Complete(
        long id,
        [FromBody] CompleteShipmentReturnRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.CompleteAsync(id, request, cancellationToken), "تم تسليم المرتجع للمرسل");
    }

    [HttpPost("{id:long}/cancel")]
    [HasPermission(Permissions.Returns.Manage)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentReturnDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ShipmentReturnDto>>> Cancel(
        long id,
        [FromBody] CancelShipmentReturnRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.CancelAsync(id, request, cancellationToken), "تم إلغاء المرتجع");
    }
}
