using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Accounting;

/// <summary>
/// الأبعاد التحليلية: cost centers (مراكز التكلفة), projects, vehicles, departments. A journal line carries at most one
/// value per type; the branch is a built-in dimension of every line.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/accounting/dimensions")]
[HasPermission(Permissions.Accounting.View)]
public sealed class AccountingDimensionsController : ApiControllerBase
{
    private readonly IChartOfAccountsService _chart;

    public AccountingDimensionsController(IChartOfAccountsService chart)
    {
        _chart = chart;
    }

    /// <summary>Filters: dimensionType (CostCenter, Project, Vehicle, Department), branchId, isActive. Search: code / name.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AccountingDimensionDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<AccountingDimensionDto>>>> GetAll([FromQuery] AccountingDimensionListQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _chart.GetDimensionsAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<AccountingDimensionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AccountingDimensionDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _chart.GetDimensionAsync(id, cancellationToken));
    }

    [HttpPost]
    [HasPermission(Permissions.Accounting.ManageChart)]
    [ProducesResponseType(typeof(ApiResponse<AccountingDimensionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<AccountingDimensionDto>>> Create([FromBody] SaveAccountingDimensionRequest request, CancellationToken cancellationToken)
    {
        var dimension = await _chart.CreateDimensionAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = dimension.Id, version = "1" }, dimension, "تم إنشاء البعد");
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Accounting.ManageChart)]
    [ProducesResponseType(typeof(ApiResponse<AccountingDimensionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AccountingDimensionDto>>> Update(long id, [FromBody] SaveAccountingDimensionRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _chart.UpdateDimensionAsync(id, request, cancellationToken), "تم حفظ البعد");
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Accounting.ManageChart)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _chart.DeleteDimensionAsync(id, cancellationToken);
        return OkMessage("تم حذف البعد");
    }
}
