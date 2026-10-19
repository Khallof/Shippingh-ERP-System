using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Pricing;
using ShippingERP.Application.Interfaces.Pricing;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Pricing;

/// <summary>
/// طلبات تعديل الأسعار (manual price changes). Reading requires Pricing.View; approving or rejecting requires
/// Pricing.ApproveOverride, and the approver must be another user than the requester.
/// Only requests of shipments the caller can see are listed.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/price-overrides")]
[HasPermission(Permissions.Pricing.View)]
public sealed class PriceOverridesController : ApiControllerBase
{
    private readonly IShipmentPricingService _pricing;

    public PriceOverridesController(IShipmentPricingService pricing)
    {
        _pricing = pricing;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PriceOverrideDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<PriceOverrideDto>>>> GetAll(
        [FromQuery] PriceOverrideListQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _pricing.GetOverridesAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PriceOverrideDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PriceOverrideDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _pricing.GetOverrideAsync(id, cancellationToken));
    }

    /// <summary>Applies the requested price to the shipment (new pricing snapshot).</summary>
    [HttpPost("{id:long}/approve")]
    [HasPermission(Permissions.Pricing.ApproveOverride)]
    [ProducesResponseType(typeof(ApiResponse<ShipmentPriceResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ShipmentPriceResultDto>>> Approve(
        long id,
        [FromBody] DecidePriceOverrideRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _pricing.ApproveOverrideAsync(id, request, cancellationToken), "تم اعتماد السعر وتطبيقه على الشحنة");
    }

    /// <summary>Rejects the request (Notes = the reason, required). The shipment keeps its price.</summary>
    [HttpPost("{id:long}/reject")]
    [HasPermission(Permissions.Pricing.ApproveOverride)]
    [ProducesResponseType(typeof(ApiResponse<PriceOverrideDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<PriceOverrideDto>>> Reject(
        long id,
        [FromBody] DecidePriceOverrideRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _pricing.RejectOverrideAsync(id, request, cancellationToken), "تم رفض طلب تعديل السعر");
    }
}
