using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Reports;
using ShippingERP.Application.Interfaces.Reports;

namespace ShippingERP.API.Controllers.V1.Reports;

/// <summary>
/// لوحة المعلومات (step 8): one call for the home screen of the web app. Every signed-in user may call it; each
/// section is filled only when the user has its permission (Shipments.View, COD.View, Cashboxes.View,
/// Reports.ViewFinancial / Invoices.View), and the branch rules of the detailed screens apply.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dashboard")]
public sealed class DashboardController : ApiControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    /// <summary>Today's shipments, statuses, delivered / failed %, COD, cashboxes, month revenue / expenses / profit,
    /// overdue invoices, driver balances, top customers and the latest shipment events.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<DashboardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> Get([FromQuery] DashboardQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _dashboard.GetAsync(query, cancellationToken));
    }
}
