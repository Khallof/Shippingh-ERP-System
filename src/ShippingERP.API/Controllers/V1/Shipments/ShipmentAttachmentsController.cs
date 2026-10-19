using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Models;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Shipments;

/// <summary>
/// Files of a shipment (مرفقات الشحنة): JPEG, PNG, WEBP or PDF up to 5 MB. Files are stored under generated names
/// outside the web root and are downloaded only through this API, with the same visibility as the shipment.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shipments/{shipmentId:long}/attachments")]
[HasPermission(Permissions.Shipments.View)]
public sealed class ShipmentAttachmentsController : ApiControllerBase
{
    private const long UploadRequestLimit = 6 * 1024 * 1024;

    private readonly IShipmentAttachmentService _service;

    public ShipmentAttachmentsController(IShipmentAttachmentService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ShipmentAttachmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ShipmentAttachmentDto>>>> GetAll(long shipmentId, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetAllAsync(shipmentId, cancellationToken));
    }

    /// <summary>Uploads a file (multipart/form-data: file, attachmentType, description, damageCaseId).</summary>
    [HttpPost]
    [HasPermission(Permissions.Shipments.Update, Permissions.DamageCases.Manage)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimit)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentAttachmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ShipmentAttachmentDto>>> Upload(
        long shipmentId,
        [FromForm] UploadShipmentAttachmentForm form,
        CancellationToken cancellationToken)
    {
        var file = form.File.ToUploadedFile()
                   ?? throw new RequestValidationException(new Dictionary<string, string[]>
                   {
                       ["file"] = new[] { "الملف مطلوب." }
                   });

        return OkResponse(await _service.UploadAsync(shipmentId, form, file, cancellationToken), "تم رفع الملف");
    }

    /// <summary>Downloads the file.</summary>
    [HttpGet("{attachmentId:long}/content")]
    [Produces("application/octet-stream", "application/json")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(long shipmentId, long attachmentId, CancellationToken cancellationToken)
    {
        return FileDownloadResult(await _service.DownloadAsync(shipmentId, attachmentId, cancellationToken));
    }

    /// <summary>Soft delete (proof-of-delivery files cannot be deleted).</summary>
    [HttpDelete("{attachmentId:long}")]
    [HasPermission(Permissions.Shipments.Update)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long shipmentId, long attachmentId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(shipmentId, attachmentId, cancellationToken);
        return OkMessage("تم حذف المرفق");
    }
}
