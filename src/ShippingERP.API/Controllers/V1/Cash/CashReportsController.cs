using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Application.Interfaces.Cod;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Cash;

/// <summary>
/// تقارير النقدية والتحصيل (step 6). Same "/reports" prefix as the financial reports of step 5, each report with
/// its own permission: the daily cash movement needs Cashboxes.View; the COD reports need COD.View.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports")]
public sealed class CashReportsController : ApiControllerBase
{
    private readonly ICashReportService _cash;
    private readonly ICodReportService _cod;

    public CashReportsController(ICashReportService cash, ICodReportService cod)
    {
        _cash = cash;
        _cod = cod;
    }

    /// <summary>
    /// حركة النقدية اليومية: one row per cashbox and local business day (max 62 days): opening balance, driver
    /// handovers, counter collections, receipts, transfers, settlement payments, other payments, closing balance and
    /// the daily count result, with totals per currency.
    /// </summary>
    [HttpGet("daily-cash-movement")]
    [HasPermission(Permissions.Cashboxes.View)]
    [ProducesResponseType(typeof(ApiResponse<DailyCashMovementReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<DailyCashMovementReportDto>>> DailyCashMovement(
        [FromQuery] DailyCashMovementQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _cash.GetDailyCashMovementAsync(query, cancellationToken));
    }

    /// <summary>تقرير تحصيل وتسليم السائقين: per driver, opening balance, collections, handovers, adjustments and closing balance.</summary>
    [HttpGet("driver-collections")]
    [HasPermission(Permissions.Cod.View)]
    [ProducesResponseType(typeof(ApiResponse<DriverCollectionReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<DriverCollectionReportDto>>> DriverCollections(
        [FromQuery] DriverCollectionReportQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _cod.GetDriverCollectionReportAsync(query, cancellationToken));
    }

    /// <summary>تقرير تسويات المرسلين: settlements of a period (default: paid, last 30 days) with the deduction totals.</summary>
    [HttpGet("sender-settlements")]
    [HasPermission(Permissions.Cod.View)]
    [ProducesResponseType(typeof(ApiResponse<SenderSettlementReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<SenderSettlementReportDto>>> SenderSettlements(
        [FromQuery] SenderSettlementReportQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _cod.GetSenderSettlementReportAsync(query, cancellationToken));
    }
}
