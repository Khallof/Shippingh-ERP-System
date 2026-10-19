using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Extensions;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Pricing;
using ShippingERP.Application.Interfaces.Pricing;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Pricing;

/// <summary>
/// تسعير شحنة قائمة (Pricing of an existing shipment).
///
/// - calculate-price: shows the full breakdown with the current rules (and optional manual values); nothing is saved.
/// - apply-price: saves the new price. A manual price / discount needs Shipments.OverridePrice and a reason; a change
///   above the approval threshold by a user without Pricing.ApproveOverride is stored as a pending request (202).
/// Cancelled or invoiced shipments cannot be repriced (422).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shipments/{shipmentId:long}")]
[HasPermission(Permissions.Shipments.View)]
public sealed class ShipmentPricingController : ApiControllerBase
{
    private readonly IShipmentPricingService _pricing;

    public ShipmentPricingController(IShipmentPricingService pricing)
    {
        _pricing = pricing;
    }

    /// <summary>Price breakdown preview: base, weight, service fees, discount, tax, total shipping charge, COD commission, net total.</summary>
    [HttpPost("calculate-price")]
    [ProducesResponseType(typeof(ApiResponse<ShipmentPriceResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ShipmentPriceResultDto>>> Calculate(
        long shipmentId,
        [FromBody] ShipmentPriceRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _pricing.CalculateAsync(shipmentId, request, cancellationToken));
    }

    /// <summary>
    /// Applies the price. 200 = applied (new pricing snapshot); 202 = the manual change waits for approval
    /// (GET /api/v1/price-overrides?status=Pending).
    /// </summary>
    [HttpPost("apply-price")]
    [HasPermission(Permissions.Shipments.Update)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentPriceResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentPriceResultDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ShipmentPriceResultDto>>> Apply(
        long shipmentId,
        [FromBody] ShipmentPriceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _pricing.ApplyAsync(shipmentId, request, cancellationToken);
        if (result.Applied)
        {
            return OkResponse(result, "تم تطبيق السعر");
        }

        return Accepted(ApiResponse<ShipmentPriceResultDto>.Ok(
            result,
            "تم تسجيل طلب تعديل السعر، وهو بانتظار موافقة مستخدم يملك صلاحية Pricing.ApproveOverride",
            HttpContext.GetTraceId()));
    }
}
