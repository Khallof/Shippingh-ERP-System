using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Customers;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.MasterData;

/// <summary>
/// Price rules of a pricing plan (قواعد التسعير), step 5 engine: conditions (zones, cities, service type, weight
/// range, package count), base price + weight tiers, per-package / COD / insurance / pickup / packaging /
/// remote area / return fees, contract discount and tax rate.
/// Reading requires Pricing.View; changing requires Pricing.Manage.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/pricing-plans/{planId:long}/rules")]
[HasPermission(Permissions.Pricing.View)]
public sealed class PricingRulesController : ApiControllerBase
{
    private readonly IPricingRuleService _service;

    public PricingRulesController(IPricingRuleService service)
    {
        _service = service;
    }

    /// <summary>All rules of the plan, highest priority first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PricingRuleDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PricingRuleDto>>>> GetAll(long planId, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetAllAsync(planId, cancellationToken));
    }

    [HttpGet("{ruleId:long}")]
    [ProducesResponseType(typeof(ApiResponse<PricingRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PricingRuleDto>>> GetById(long planId, long ruleId, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetByIdAsync(planId, ruleId, cancellationToken));
    }

    /// <summary>
    /// Adds a rule. 409 when an active rule of the plan has the same priority, the same conditions and an
    /// overlapping weight range (the engine could not choose between them).
    /// </summary>
    [HttpPost]
    [HasPermission(Permissions.Pricing.Manage)]
    [ProducesResponseType(typeof(ApiResponse<PricingRuleDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PricingRuleDto>>> Create(
        long planId,
        [FromBody] SavePricingRuleRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(planId, request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { planId, ruleId = created.Id, version = "1" }, created, "تمت إضافة قاعدة التسعير");
    }

    [HttpPut("{ruleId:long}")]
    [HasPermission(Permissions.Pricing.Manage)]
    [ProducesResponseType(typeof(ApiResponse<PricingRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PricingRuleDto>>> Update(
        long planId,
        long ruleId,
        [FromBody] SavePricingRuleRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.UpdateAsync(planId, ruleId, request, cancellationToken), "تم حفظ قاعدة التسعير");
    }

    [HttpPatch("{ruleId:long}/activate")]
    [HasPermission(Permissions.Pricing.Manage)]
    [ProducesResponseType(typeof(ApiResponse<PricingRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PricingRuleDto>>> Activate(long planId, long ruleId, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.SetActiveAsync(planId, ruleId, true, cancellationToken), "تم التفعيل");
    }

    [HttpPatch("{ruleId:long}/deactivate")]
    [HasPermission(Permissions.Pricing.Manage)]
    [ProducesResponseType(typeof(ApiResponse<PricingRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PricingRuleDto>>> Deactivate(long planId, long ruleId, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.SetActiveAsync(planId, ruleId, false, cancellationToken), "تم الإيقاف");
    }

    [HttpDelete("{ruleId:long}")]
    [HasPermission(Permissions.Pricing.Manage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long planId, long ruleId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(planId, ruleId, cancellationToken);
        return OkMessage("تم حذف قاعدة التسعير");
    }
}
