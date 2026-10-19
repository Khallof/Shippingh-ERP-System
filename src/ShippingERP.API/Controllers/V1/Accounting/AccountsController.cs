using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Accounting;

/// <summary>
/// دليل الحسابات (chart of accounts): a tree of header accounts (totals only) and postable accounts. Reading needs
/// Accounting.View; changes need Accounting.ManageChart. System accounts (seeded and used by the posting rules)
/// can be renamed but never deleted; an account with journal lines keeps its type.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/accounting/accounts")]
[HasPermission(Permissions.Accounting.View)]
public sealed class AccountsController : ApiControllerBase
{
    private readonly IChartOfAccountsService _chart;

    public AccountsController(IChartOfAccountsService chart)
    {
        _chart = chart;
    }

    /// <summary>Accounts. Filters: accountType, accountCategoryId, parentAccountId, isPostable, isActive. Search: code (prefix) / name.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AccountDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<AccountDto>>>> GetAll([FromQuery] AccountListQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _chart.GetAccountsAsync(query, cancellationToken));
    }

    /// <summary>The chart as a tree (the "chart of accounts" report). includeBalances=true adds the natural balance of every node at asOfDate.</summary>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AccountTreeNodeDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AccountTreeNodeDto>>>> GetTree([FromQuery] AccountTreeQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _chart.GetTreeAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AccountDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _chart.GetAccountAsync(id, cancellationToken));
    }

    [HttpPost]
    [HasPermission(Permissions.Accounting.ManageChart)]
    [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<AccountDto>>> Create([FromBody] SaveAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await _chart.CreateAccountAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = account.Id, version = "1" }, account, "تم إنشاء الحساب");
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Accounting.ManageChart)]
    [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<AccountDto>>> Update(long id, [FromBody] SaveAccountRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _chart.UpdateAccountAsync(id, request, cancellationToken), "تم حفظ الحساب");
    }

    /// <summary>Soft delete of an unused, non-system account (no children, lines, rules or bank accounts).</summary>
    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Accounting.ManageChart)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _chart.DeleteAccountAsync(id, cancellationToken);
        return OkMessage("تم حذف الحساب");
    }
}
