using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Accounting;

/// <summary>
/// الفترات المحاسبية (one per month). Nothing can be posted into a closed period, manually or automatically: the
/// closing waits for the postings in progress (row lock) and every later posting is refused. A period closes only
/// when nothing is left behind (GET {id}/close-check lists what blocks it).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/accounting/periods")]
[HasPermission(Permissions.Accounting.View)]
public sealed class AccountingPeriodsController : ApiControllerBase
{
    private readonly IFiscalPeriodService _periods;

    public AccountingPeriodsController(IFiscalPeriodService periods)
    {
        _periods = periods;
    }

    /// <summary>Filters: fiscalYearId, status (Open, Closed), date (the period containing it).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AccountingPeriodDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<AccountingPeriodDto>>>> GetAll([FromQuery] AccountingPeriodListQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _periods.GetPeriodsAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<AccountingPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AccountingPeriodDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _periods.GetPeriodAsync(id, cancellationToken));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Accounting.ManagePeriods)]
    [ProducesResponseType(typeof(ApiResponse<AccountingPeriodDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AccountingPeriodDto>>> Update(long id, [FromBody] UpdateAccountingPeriodRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _periods.UpdatePeriodAsync(id, request, cancellationToken), "تم حفظ الفترة");
    }

    /// <summary>What still blocks the closing: unposted / waiting manual entries, operational events not posted, failed postings.</summary>
    [HttpGet("{id:long}/close-check")]
    [ProducesResponseType(typeof(ApiResponse<PeriodCloseCheckDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PeriodCloseCheckDto>>> CloseCheck(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _periods.GetCloseCheckAsync(id, cancellationToken));
    }

    [HttpPost("{id:long}/close")]
    [HasPermission(Permissions.Accounting.ManagePeriods)]
    [ProducesResponseType(typeof(ApiResponse<AccountingPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<AccountingPeriodDto>>> Close(long id, [FromBody] CloseAccountingPeriodRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _periods.ClosePeriodAsync(id, request, cancellationToken), "تم إقفال الفترة المحاسبية");
    }

    /// <summary>Reopens a closed period (reason required; not inside a closed fiscal year).</summary>
    [HttpPost("{id:long}/reopen")]
    [HasPermission(Permissions.Accounting.ManagePeriods)]
    [ProducesResponseType(typeof(ApiResponse<AccountingPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<AccountingPeriodDto>>> Reopen(long id, [FromBody] ReopenAccountingPeriodRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _periods.ReopenPeriodAsync(id, request, cancellationToken), "تم إعادة فتح الفترة المحاسبية");
    }
}
