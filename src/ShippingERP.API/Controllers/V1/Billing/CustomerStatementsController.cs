using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Application.Interfaces.Billing;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Billing;

/// <summary>
/// كشف حساب العميل (Customer account statement) from the customer sub-ledger: opening balance, every invoice,
/// payment, credit note and cancellation with a running balance, closing balance and open documents.
/// Requires Receivables.View. Branch users see only their branch's documents.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/customers/{customerId:long}/statement")]
[HasPermission(Permissions.Receivables.View)]
public sealed class CustomerStatementsController : ApiControllerBase
{
    private readonly IReceivablesReportService _reports;

    public CustomerStatementsController(IReceivablesReportService reports)
    {
        _reports = reports;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CustomerStatementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CustomerStatementDto>>> Get(
        long customerId,
        [FromQuery] CustomerStatementQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetStatementAsync(customerId, query, cancellationToken));
    }

    /// <summary>Step 9: what the customer owes now, per currency (positive = debt).</summary>
    [HttpGet("~/api/v{version:apiVersion}/customers/{customerId:long}/balances")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CustomerBalanceDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CustomerBalanceDto>>>> Balances(long customerId, CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetBalancesAsync(customerId, cancellationToken));
    }

    /// <summary>The whole period as an Excel file (paging is ignored).</summary>
    [HttpGet("export")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Export(long customerId, [FromQuery] CustomerStatementQuery query, CancellationToken cancellationToken)
    {
        return FileDownloadResult(await _reports.ExportStatementAsync(customerId, query, cancellationToken));
    }
}
