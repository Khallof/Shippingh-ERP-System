using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Shipments;

/// <summary>
/// إعدادات الشحن: volumetric divisor, weight rounding, tracking number prefix, label size, tracking URL.
/// Reading requires a signed-in user; changing requires MasterData.Manage.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shipping-settings")]
public sealed class ShippingSettingsController : ApiControllerBase
{
    private readonly IShippingSettingsService _service;

    public ShippingSettingsController(IShippingSettingsService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ShippingSettingsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ShippingSettingsDto>>> Get(CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetAsync(cancellationToken));
    }

    /// <summary>Saves the settings. Existing shipments keep their prices (pricing snapshots).</summary>
    [HttpPut]
    [HasPermission(Permissions.MasterData.Manage)]
    [ProducesResponseType(typeof(ApiResponse<ShippingSettingsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ShippingSettingsDto>>> Save(
        [FromBody] SaveShippingSettingsRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.SaveAsync(request, cancellationToken), "تم حفظ الإعدادات");
    }
}
