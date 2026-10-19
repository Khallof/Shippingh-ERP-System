using ShippingERP.Application.Common.Files;
using ShippingERP.Application.DTOs.Shipments;

namespace ShippingERP.API.Models;

// multipart/form-data bodies. They inherit the Application request, so the same FluentValidation validator runs
// (ValidationFilter also looks for validators of base types). The files are checked by the file storage.

/// <summary>POST /api/v1/shipments/{id}/deliver (multipart/form-data).</summary>
public sealed class DeliverShipmentForm : DeliverShipmentRequest
{
    /// <summary>Optional recipient signature (PNG / JPEG / WEBP, max 5 MB).</summary>
    public IFormFile? Signature { get; set; }

    /// <summary>Optional delivery photo (PNG / JPEG / WEBP, max 5 MB).</summary>
    public IFormFile? Photo { get; set; }
}

/// <summary>POST /api/v1/shipments/{id}/attachments (multipart/form-data).</summary>
public sealed class UploadShipmentAttachmentForm : UploadShipmentAttachmentRequest
{
    /// <summary>JPEG, PNG, WEBP or PDF, max 5 MB.</summary>
    public IFormFile? File { get; set; }
}

/// <summary>POST /api/v1/shipments/import and /import/preview (multipart/form-data).</summary>
public sealed class ImportShipmentsForm : ImportShipmentsRequest
{
    /// <summary>The .xlsx file (max 5 MB, 500 rows).</summary>
    public IFormFile? File { get; set; }
}

public static class FormFileExtensions
{
    /// <summary>IFormFile -> UploadedFile (the Application layer does not know ASP.NET Core).</summary>
    public static UploadedFile? ToUploadedFile(this IFormFile? file) =>
        file is null ? null : new UploadedFile(file.FileName, file.ContentType, file.Length, file.OpenReadStream);
}
