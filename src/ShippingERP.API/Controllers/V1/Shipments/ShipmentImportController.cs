using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Models;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Shipments;

/// <summary>
/// استيراد الشحنات من Excel. 1) Download the template. 2) Preview: row-by-row validation, nothing saved.
/// 3) Import: all rows in one transaction; if any row is invalid nothing is imported (400 with the row errors).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shipments/import")]
[HasPermission(Permissions.Shipments.Import)]
public sealed class ShipmentImportController : ApiControllerBase
{
    private const long ImportRequestLimit = 6 * 1024 * 1024;

    private readonly IShipmentImportService _service;

    public ShipmentImportController(IShipmentImportService service)
    {
        _service = service;
    }

    /// <summary>The .xlsx template (columns, an example row and Arabic instructions).</summary>
    [HttpGet("template")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    public IActionResult GetTemplate()
    {
        return FileDownloadResult(_service.GetTemplate());
    }

    /// <summary>Validates every row and returns the preview (multipart/form-data: file, originBranchId).</summary>
    [HttpPost("preview")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImportRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImportRequestLimit)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentImportPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ShipmentImportPreviewDto>>> Preview(
        [FromForm] ImportShipmentsForm form,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.PreviewAsync(RequireFile(form), form, cancellationToken));
    }

    /// <summary>Imports every row in ONE transaction (multipart/form-data: file, originBranchId).</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImportRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImportRequestLimit)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentImportResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ShipmentImportResultDto>>> Import(
        [FromForm] ImportShipmentsForm form,
        CancellationToken cancellationToken)
    {
        var result = await _service.ImportAsync(RequireFile(form), form, cancellationToken);
        return OkResponse(result, $"تم استيراد {result.ImportedCount} شحنة");
    }

    private static UploadedFile RequireFile(ImportShipmentsForm form) =>
        form.File.ToUploadedFile()
        ?? throw new RequestValidationException(new Dictionary<string, string[]>
        {
            ["file"] = new[] { "ملف Excel مطلوب." }
        });
}
