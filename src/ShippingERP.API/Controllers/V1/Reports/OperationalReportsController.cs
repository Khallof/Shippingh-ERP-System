using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Reports;
using ShippingERP.Application.Interfaces.Reports;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Reports;

/// <summary>
/// التقارير التشغيلية (step 8): shipments created in a period by status, branch, driver, sender, service and day.
/// Needs Reports.ViewOperational and Shipments.View; the shipment visibility rules apply.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports/operations")]
[HasPermission(Permissions.Reports.ViewOperational)]
[HasPermission(Permissions.Shipments.View)]
public sealed class OperationalReportsController : ApiControllerBase
{
    private readonly IOperationalReportService _reports;
    private readonly IReportExportService _exports;

    public OperationalReportsController(IOperationalReportService reports, IReportExportService exports)
    {
        _reports = reports;
        _exports = exports;
    }

    /// <summary>Filters: fromDate, toDate (local dates, default the last 30 days), branchId, senderCustomerId, driverId, status.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<OperationalReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<OperationalReportDto>>> Get([FromQuery] OperationalReportQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetAsync(query, cancellationToken));
    }

    /// <summary>The same report as Excel (?format=excel) or PDF (?format=pdf).</summary>
    [HttpGet("export")]
    [Produces("application/pdf", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] OperationalReportQuery query, [FromQuery] ExportQuery export, CancellationToken cancellationToken)
    {
        return FileDownloadResult(await _exports.OperationalReportAsync(query, export.Format, cancellationToken));
    }
}
