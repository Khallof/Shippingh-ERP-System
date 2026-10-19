using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Shipments;

/// <summary>
/// بيانات حركة المستودع (warehouse check-in / check-out). Draft -> add/remove shipments (scan tracking numbers)
/// -> confirm (all status changes in one transaction). Requires Warehouse.ManageMovements.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/warehouse-manifests")]
[HasPermission(Permissions.Warehouse.ManageMovements)]
public sealed class WarehouseManifestsController : ApiControllerBase
{
    private readonly IWarehouseManifestService _service;

    public WarehouseManifestsController(IWarehouseManifestService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<WarehouseManifestDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<WarehouseManifestDto>>>> GetAll(
        [FromQuery] WarehouseManifestListQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetPagedAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseManifestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<WarehouseManifestDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<WarehouseManifestDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<WarehouseManifestDto>>> Create(
        [FromBody] CreateWarehouseManifestRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = created.Id, version = "1" }, created, "تم إنشاء البيان");
    }

    /// <summary>Adds shipments by tracking number / shipment number / package barcode.</summary>
    [HttpPost("{id:long}/items")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseManifestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<WarehouseManifestDto>>> AddShipments(
        long id,
        [FromBody] ShipmentCodesRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.AddShipmentsAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:long}/items/{shipmentId:long}")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseManifestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<WarehouseManifestDto>>> RemoveShipment(long id, long shipmentId, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.RemoveShipmentAsync(id, shipmentId, cancellationToken));
    }

    [HttpPost("{id:long}/confirm")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseManifestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<WarehouseManifestDto>>> Confirm(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.ConfirmAsync(id, cancellationToken), "تم تأكيد البيان");
    }

    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<WarehouseManifestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<WarehouseManifestDto>>> Cancel(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.CancelAsync(id, cancellationToken), "تم إلغاء البيان");
    }
}
