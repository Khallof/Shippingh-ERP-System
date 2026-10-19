using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
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
}
