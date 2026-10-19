using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Application.Interfaces.Billing;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Billing;

/// <summary>التقارير المالية (Financial reports). Requires Reports.ViewFinancial.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports")]
[HasPermission(Permissions.Reports.ViewFinancial)]
public sealed class ReportsController : ApiControllerBase
{
    private readonly IReceivablesReportService _reports;

    public ReportsController(IReceivablesReportService reports)
    {
        _reports = reports;
    }

    /// <summary>
    /// أعمار الذمم (customer aging): open invoice balances by days past due (current, 1-30, 31-60, 61-90, 90+),
    /// unapplied credits and net balance per customer, as of a date, in one currency, with grand totals.
    /// </summary>
    [HttpGet("customer-aging")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAgingReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CustomerAgingReportDto>>> CustomerAging(
        [FromQuery] CustomerAgingQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetCustomerAgingAsync(query, cancellationToken));
    }
}
