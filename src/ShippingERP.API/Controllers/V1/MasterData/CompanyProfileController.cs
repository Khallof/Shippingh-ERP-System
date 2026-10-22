using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Models;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.MasterData;

/// <summary>
/// Company profile (بيانات الشركة): one record printed on invoices and waybills.
/// Reading requires a signed-in user; changing requires MasterData.Manage.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/company-profile")]
public sealed class CompanyProfileController : ApiControllerBase
{
    private readonly ICompanyProfileService _service;

    public CompanyProfileController(ICompanyProfileService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CompanyProfileDto>>> Get(CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetAsync(cancellationToken));
    }

    /// <summary>Creates the profile the first time, updates it afterwards.</summary>
    [HttpPut]
    [HasPermission(Permissions.MasterData.Manage)]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CompanyProfileDto>>> Save(
        [FromBody] SaveCompanyProfileRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.SaveAsync(request, cancellationToken), "تم حفظ بيانات الشركة");
    }

    // ------------------------------------------------------------------ logo (step 9)

    /// <summary>The company logo (public: shown on the login page and printed on documents). 404 when none.</summary>
    [HttpGet("logo")]
    [AllowAnonymous]
    [Produces("image/png", "image/jpeg", "application/json")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogo(CancellationToken cancellationToken)
    {
        var logo = await _service.GetLogoAsync(cancellationToken)
                   ?? throw new NotFoundException("لا يوجد شعار للشركة.");

        // The app adds ?v=<LogoVersion> to the URL, so a cached copy is replaced as soon as the logo changes.
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(logo.Content, logo.ContentType);
    }

    /// <summary>Uploads / replaces the logo (PNG or JPG, max 1 MB).</summary>
    [HttpPut("logo")]
    [HasPermission(Permissions.MasterData.Manage)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CompanyProfileDto>>> UploadLogo([FromForm] CompanyLogoForm form, CancellationToken cancellationToken)
    {
        var file = form.File.ToUploadedFile()
                   ?? throw new RequestValidationException(new Dictionary<string, string[]> { ["file"] = new[] { "اختر صورة الشعار." } });

        return OkResponse(await _service.SetLogoAsync(file, cancellationToken), "تم حفظ الشعار");
    }

    [HttpDelete("logo")]
    [HasPermission(Permissions.MasterData.Manage)]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CompanyProfileDto>>> RemoveLogo(CancellationToken cancellationToken)
    {
        return OkResponse(await _service.RemoveLogoAsync(cancellationToken), "تم حذف الشعار");
    }
}
