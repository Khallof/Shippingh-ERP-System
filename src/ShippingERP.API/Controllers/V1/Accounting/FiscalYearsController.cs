using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Accounting;

/// <summary>
/// السنوات المالية: creating a year creates its monthly accounting periods. A year with journal entries is never
/// deleted; it is closed once all its periods are closed.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/accounting/fiscal-years")]
[HasPermission(Permissions.Accounting.View)]
public sealed class FiscalYearsController : ApiControllerBase
{
    private readonly IFiscalPeriodService _periods;

    public FiscalYearsController(IFiscalPeriodService periods)
    {
        _periods = periods;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FiscalYearDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FiscalYearDto>>>> GetAll(CancellationToken cancellationToken)
    {
        return OkResponse(await _periods.GetFiscalYearsAsync(cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<FiscalYearDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<FiscalYearDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _periods.GetFiscalYearAsync(id, cancellationToken));
    }

    /// <summary>Default end date: one year after the start minus one day. Default code: FY + start year.</summary>
    [HttpPost]
    [HasPermission(Permissions.Accounting.ManagePeriods)]
    [ProducesResponseType(typeof(ApiResponse<FiscalYearDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<FiscalYearDto>>> Create([FromBody] CreateFiscalYearRequest request, CancellationToken cancellationToken)
    {
        var year = await _periods.CreateFiscalYearAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = year.Id, version = "1" }, year, "تم إنشاء السنة المالية وفتراتها");
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Accounting.ManagePeriods)]
    [ProducesResponseType(typeof(ApiResponse<FiscalYearDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<FiscalYearDto>>> Update(long id, [FromBody] UpdateFiscalYearRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _periods.UpdateFiscalYearAsync(id, request, cancellationToken), "تم حفظ السنة المالية");
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Accounting.ManagePeriods)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _periods.DeleteFiscalYearAsync(id, cancellationToken);
        return OkMessage("تم حذف السنة المالية");
    }

    [HttpPost("{id:long}/close")]
    [HasPermission(Permissions.Accounting.ManagePeriods)]
    [ProducesResponseType(typeof(ApiResponse<FiscalYearDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<FiscalYearDto>>> Close(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _periods.CloseFiscalYearAsync(id, cancellationToken), "تم إقفال السنة المالية");
    }
}
