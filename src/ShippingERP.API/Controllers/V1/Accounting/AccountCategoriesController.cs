using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Accounting;

/// <summary>تصنيفات الحسابات: the groups of the financial statements and the cash flow section of their accounts.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/accounting/account-categories")]
[HasPermission(Permissions.Accounting.View)]
public sealed class AccountCategoriesController : ApiControllerBase
{
    private readonly IChartOfAccountsService _chart;

    public AccountCategoriesController(IChartOfAccountsService chart)
    {
        _chart = chart;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AccountCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AccountCategoryDto>>>> GetAll(CancellationToken cancellationToken)
    {
        return OkResponse(await _chart.GetCategoriesAsync(cancellationToken));
    }

    [HttpPost]
    [HasPermission(Permissions.Accounting.ManageChart)]
    [ProducesResponseType(typeof(ApiResponse<AccountCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<AccountCategoryDto>>> Create([FromBody] SaveAccountCategoryRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _chart.CreateCategoryAsync(request, cancellationToken), "تم إنشاء التصنيف");
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Accounting.ManageChart)]
    [ProducesResponseType(typeof(ApiResponse<AccountCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AccountCategoryDto>>> Update(long id, [FromBody] SaveAccountCategoryRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _chart.UpdateCategoryAsync(id, request, cancellationToken), "تم حفظ التصنيف");
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Accounting.ManageChart)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _chart.DeleteCategoryAsync(id, cancellationToken);
        return OkMessage("تم حذف التصنيف");
    }
}
