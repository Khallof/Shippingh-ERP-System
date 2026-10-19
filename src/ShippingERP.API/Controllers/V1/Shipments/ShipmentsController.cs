using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Models;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Reports;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Reports;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Shipments;

/// <summary>
/// الشحنات (Shipments): registration, search, tracking, status, driver assignment, delivery and labels.
///
/// Visibility: users without Branches.ViewAll see shipments whose origin, current, destination or return branch is
/// their branch; a driver (Driver role without Shipments.AssignDriver) sees only shipments assigned to him.
/// Status changes follow the shipment state machine; every change is kept in the history.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shipments")]
[HasPermission(Permissions.Shipments.View)]
public sealed class ShipmentsController : ApiControllerBase
{
    /// <summary>Signature + photo (2 x 5 MB) and the form fields.</summary>
    private const long DeliveryRequestLimit = 12 * 1024 * 1024;

    private readonly IShipmentService _shipments;
    private readonly IDeliveryService _delivery;
    private readonly IShipmentLabelService _labels;

    public ShipmentsController(IShipmentService shipments, IDeliveryService delivery, IShipmentLabelService labels)
    {
        _shipments = shipments;
        _delivery = delivery;
        _labels = labels;
    }

    /// <summary>
    /// Search / filter / sort / page. Search covers tracking number, shipment number, external reference and
    /// sender / recipient names and phones.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ShipmentListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<ShipmentListItemDto>>>> GetAll(
        [FromQuery] ShipmentListQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _shipments.GetPagedAsync(query, cancellationToken));
    }

    /// <summary>
    /// Step 8: the filtered list (same filters and visibility as GET /shipments, every page, up to 10,000 rows) as
    /// Excel (?format=excel) or PDF (?format=pdf).
    /// </summary>
    [HttpGet("export")]
    [Produces("application/pdf", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Export(
        [FromQuery] ShipmentListQuery query,
        [FromQuery] ExportQuery export,
        [FromServices] IReportExportService exports,
        CancellationToken cancellationToken)
    {
        return FileDownloadResult(await exports.ShipmentsAsync(query, export.Format, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ShipmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ShipmentDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _shipments.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>Tracking timeline by tracking number, shipment number or package barcode (scanner input).</summary>
    [HttpGet("track/{code}")]
    [ProducesResponseType(typeof(ApiResponse<ShipmentTrackingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ShipmentTrackingDto>>> Track(string code, CancellationToken cancellationToken)
    {
        return OkResponse(await _shipments.TrackAsync(code, cancellationToken));
    }

    /// <summary>Full, immutable status history (who, when, where, why, which document).</summary>
    [HttpGet("{id:long}/history")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ShipmentStatusHistoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ShipmentStatusHistoryDto>>>> GetHistory(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _shipments.GetHistoryAsync(id, cancellationToken));
    }

    /// <summary>Every pricing snapshot of the shipment (the last one is current).</summary>
    [HttpGet("{id:long}/pricing")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ShipmentPricingDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ShipmentPricingDto>>>> GetPricing(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _shipments.GetPricingHistoryAsync(id, cancellationToken));
    }

    /// <summary>Calculates weights and price without saving (same body as POST).</summary>
    [HttpPost("quote")]
    [HasPermission(Permissions.Shipments.Create, Permissions.Shipments.Update)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentPricingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ShipmentPricingDto>>> Quote(
        [FromBody] SaveShipmentRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _shipments.QuoteAsync(request, cancellationToken));
    }

    /// <summary>Registers a shipment: numbers, packages, weights, price snapshot and the first history row.</summary>
    [HttpPost]
    [HasPermission(Permissions.Shipments.Create)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ShipmentDto>>> Create(
        [FromBody] SaveShipmentRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _shipments.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = created.Id, version = "1" }, created, "تم تسجيل الشحنة");
    }

    /// <summary>Edits a shipment that has not left the origin branch (send all fields). The price is recalculated.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Shipments.Update)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ShipmentDto>>> Update(
        long id,
        [FromBody] SaveShipmentRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _shipments.UpdateAsync(id, request, cancellationToken), "تم حفظ التعديلات");
    }

    /// <summary>Scan-style status change: PickedUp, ReceivedAtBranch or InWarehouse (with warehouseId).</summary>
    [HttpPatch("{id:long}/status")]
    [HasPermission(Permissions.Shipments.ChangeStatus)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ShipmentDto>>> ChangeStatus(
        long id,
        [FromBody] ChangeShipmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _shipments.ChangeStatusAsync(id, request, cancellationToken), "تم تحديث الحالة");
    }

    /// <summary>Cancels a shipment that has not left the origin branch. Reason required.</summary>
    [HttpPost("{id:long}/cancel")]
    [HasPermission(Permissions.Shipments.Cancel)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ShipmentDto>>> Cancel(
        long id,
        [FromBody] CancelShipmentRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _shipments.CancelAsync(id, request, cancellationToken), "تم إلغاء الشحنة");
    }

    /// <summary>Assigns (or removes with driverId = null) the driver and vehicle of a shipment at its current branch.</summary>
    [HttpPatch("{id:long}/driver")]
    [HasPermission(Permissions.Shipments.AssignDriver)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ShipmentDto>>> AssignDriver(
        long id,
        [FromBody] AssignDriverRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _shipments.AssignDriverAsync(id, request, cancellationToken), "تم تحديث السائق");
    }

    /// <summary>
    /// Corrects a wrong status outside the normal rules (e.g. Delivered by mistake). Reason required (10+ chars);
    /// recorded in the history with isCorrection = true and in the audit log.
    /// </summary>
    [HttpPost("{id:long}/status-corrections")]
    [HasPermission(Permissions.Shipments.CorrectStatus)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ShipmentDto>>> CorrectStatus(
        long id,
        [FromBody] CorrectShipmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _shipments.CorrectStatusAsync(id, request, cancellationToken), "تم تصحيح الحالة");
    }

    /// <summary>
    /// Confirms the delivery with a proof of delivery (multipart/form-data: recipientName, deliveredAtUtc, notes,
    /// latitude, longitude + optional files "signature" and "photo").
    /// </summary>
    [HttpPost("{id:long}/deliver")]
    [HasPermission(Permissions.Shipments.ConfirmDelivery)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(DeliveryRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = DeliveryRequestLimit)]
    [ProducesResponseType(typeof(ApiResponse<ProofOfDeliveryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ProofOfDeliveryDto>>> Deliver(
        long id,
        [FromForm] DeliverShipmentForm form,
        CancellationToken cancellationToken)
    {
        var proof = await _delivery.DeliverAsync(
            id,
            form,
            form.Signature.ToUploadedFile(),
            form.Photo.ToUploadedFile(),
            cancellationToken);

        return OkResponse(proof, "تم تأكيد التسليم");
    }

    /// <summary>Records a failed delivery attempt (shipment OutForDelivery -> DeliveryFailed).</summary>
    [HttpPost("{id:long}/delivery-failed")]
    [HasPermission(Permissions.Shipments.ConfirmDelivery)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ShipmentDto>>> DeliveryFailed(
        long id,
        [FromBody] RecordDeliveryFailureRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _delivery.RecordFailureAsync(id, request, cancellationToken), "تم تسجيل محاولة التسليم الفاشلة");
    }

    /// <summary>The latest proof of delivery (signatureUrl / photoUrl download the files).</summary>
    [HttpGet("{id:long}/proof-of-delivery")]
    [ProducesResponseType(typeof(ApiResponse<ProofOfDeliveryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ProofOfDeliveryDto>>> GetProofOfDelivery(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _delivery.GetProofOfDeliveryAsync(id, cancellationToken));
    }

    /// <summary>Label of one shipment (one page per package). format = Pdf (default) or Html.</summary>
    [HttpGet("{id:long}/label")]
    [Produces("application/pdf", "text/html", "application/json")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetLabel(long id, [FromQuery] LabelFormat format = LabelFormat.Pdf, CancellationToken cancellationToken = default)
    {
        return FileDownloadResult(await _labels.GenerateAsync(id, format, cancellationToken));
    }

    /// <summary>Labels of up to 100 shipments in one file (in the given order).</summary>
    [HttpPost("labels")]
    [Produces("application/pdf", "text/html", "application/json")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLabels([FromBody] BulkLabelRequest request, CancellationToken cancellationToken)
    {
        return FileDownloadResult(await _labels.GenerateBulkAsync(request, cancellationToken));
    }
}
