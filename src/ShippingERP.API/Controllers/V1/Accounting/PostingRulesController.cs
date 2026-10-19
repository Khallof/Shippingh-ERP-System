using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Accounting;

/// <summary>
/// قواعد الترحيل الآلي: the general ledger account of each account role of the operational events, optionally per
/// branch, currency, cashbox, bank account or expense category (the most specific rule wins, then the priority).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/accounting/posting-rules")]
[HasPermission(Permissions.Accounting.View)]
public sealed class PostingRulesController : ApiControllerBase
{
    private readonly IPostingRuleService _rules;

    public PostingRulesController(IPostingRuleService rules)
    {
        _rules = rules;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PostingRuleDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<PostingRuleDto>>>> GetAll([FromQuery] PostingRuleListQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _rules.GetPagedAsync(query, cancellationToken));
    }

    /// <summary>The account roles used by the operational events, with their Arabic names.</summary>
    [HttpGet("roles")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AccountRoleDto>>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<IReadOnlyList<AccountRoleDto>>> GetRoles()
    {
        return OkResponse(_rules.GetRoles());
    }

    /// <summary>Which account a posting line with these scopes would use now.</summary>
    [HttpGet("resolve")]
    [ProducesResponseType(typeof(ApiResponse<ResolvedPostingAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ResolvedPostingAccountDto>>> Resolve([FromQuery] ResolvePostingAccountQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _rules.ResolveAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PostingRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PostingRuleDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _rules.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [HasPermission(Permissions.Accounting.ManageChart)]
    [ProducesResponseType(typeof(ApiResponse<PostingRuleDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<PostingRuleDto>>> Create([FromBody] SavePostingRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await _rules.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = rule.Id, version = "1" }, rule, "تم حفظ قاعدة الترحيل");
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Accounting.ManageChart)]
    [ProducesResponseType(typeof(ApiResponse<PostingRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PostingRuleDto>>> Update(long id, [FromBody] SavePostingRuleRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _rules.UpdateAsync(id, request, cancellationToken), "تم حفظ قاعدة الترحيل");
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Accounting.ManageChart)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _rules.DeleteAsync(id, cancellationToken);
        return OkMessage("تم حذف قاعدة الترحيل");
    }
}
