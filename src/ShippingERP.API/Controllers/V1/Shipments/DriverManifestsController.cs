using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Reports;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Shipments;

/// <summary>
/// بيانات التوزيع للسائقين (driver delivery manifests).
/// Reading requires Shipments.View: a driver sees only his own manifests (the driver app list).
/// Creating, dispatching, closing and cancelling require DeliveryManifests.Manage.
/// Deliveries are confirmed with POST /shipments/{id}/deliver or /delivery-failed.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/driver-manifests")]
[HasPermission(Permissions.Shipments.View)]
public sealed class DriverManifestsController : ApiControllerBase
{
    private readonly IDriverManifestService _service;

    public DriverManifestsController(IDriverManifestService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DriverManifestDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<DriverManifestDto>>>> GetAll(
        [FromQuery] DriverManifestListQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetPagedAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<DriverManifestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DriverManifestDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>Step 8: the printable delivery manifest (A4 landscape PDF with the signature boxes).</summary>
    [HttpGet("{id:long}/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pdf(long id, [FromServices] IReportExportService exports, CancellationToken cancellationToken)
    {
        return FileDownloadResult(await exports.DriverManifestPdfAsync(id, cancellationToken));
    }

    [HttpPost]
    [HasPermission(Permissions.DeliveryManifests.Manage)]
    [ProducesResponseType(typeof(ApiResponse<DriverManifestDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<DriverManifestDto>>> Create(
        [FromBody] CreateDriverManifestRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = created.Id, version = "1" }, created, "تم إنشاء بيان التوزيع");
    }

    [HttpPost("{id:long}/items")]
    [HasPermission(Permissions.DeliveryManifests.Manage)]
    [ProducesResponseType(typeof(ApiResponse<DriverManifestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<DriverManifestDto>>> AddShipments(
        long id,
        [FromBody] ShipmentCodesRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.AddShipmentsAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:long}/items/{shipmentId:long}")]
    [HasPermission(Permissions.DeliveryManifests.Manage)]
    [ProducesResponseType(typeof(ApiResponse<DriverManifestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DriverManifestDto>>> RemoveShipment(long id, long shipmentId, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.RemoveShipmentAsync(id, shipmentId, cancellationToken));
    }

    /// <summary>Hands the manifest to the driver: shipments become OutForDelivery and are assigned to him.</summary>
    [HttpPost("{id:long}/dispatch")]
    [HasPermission(Permissions.DeliveryManifests.Manage)]
    [ProducesResponseType(typeof(ApiResponse<DriverManifestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<DriverManifestDto>>> Dispatch(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.DispatchAsync(id, cancellationToken), "تم تسليم البيان للسائق");
    }

    /// <summary>The driver is back: pending shipments become DeliveryFailed and the manifest is closed.</summary>
    [HttpPost("{id:long}/close")]
    [HasPermission(Permissions.DeliveryManifests.Manage)]
    [ProducesResponseType(typeof(ApiResponse<DriverManifestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<DriverManifestDto>>> Close(
        long id,
        [FromBody] CloseDriverManifestRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.CloseAsync(id, request, cancellationToken), "تم إغلاق البيان");
    }

    [HttpPost("{id:long}/cancel")]
    [HasPermission(Permissions.DeliveryManifests.Manage)]
    [ProducesResponseType(typeof(ApiResponse<DriverManifestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DriverManifestDto>>> Cancel(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.CancelAsync(id, cancellationToken), "تم إلغاء البيان");
    }
}
