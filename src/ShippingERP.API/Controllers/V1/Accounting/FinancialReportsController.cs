using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.DTOs.Reports;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Application.Interfaces.Reports;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Accounting;

/// <summary>
/// التقارير المالية, from the posted journal lines in the base currency. Every report needs Accounting.View and
/// Reports.ViewFinancial; users without Branches.ViewAll get their own branch only.
/// The COD aging and the operational customer aging stay at /api/v1/cod/aging and /api/v1/reports/customer-aging.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/accounting")]
[HasPermission(Permissions.Accounting.View)]
[HasPermission(Permissions.Reports.ViewFinancial)]
public sealed class FinancialReportsController : ApiControllerBase
{
    private readonly IFinancialReportService _reports;
    private readonly IReportExportService _exports;

    public FinancialReportsController(IFinancialReportService reports, IReportExportService exports)
    {
        _reports = reports;
        _exports = exports;
    }

    // ------------------------------------------------------------------ exports (step 8): ?format=excel | pdf

    /// <summary>Trial balance as Excel or PDF (same filters as GET trial-balance).</summary>
    [HttpGet("trial-balance/export")]
    [Produces("application/pdf", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
    public async Task<IActionResult> ExportTrialBalance([FromQuery] TrialBalanceQuery query, [FromQuery] ExportQuery export, CancellationToken cancellationToken) =>
        FileDownloadResult(await _exports.TrialBalanceAsync(query, export.Format, cancellationToken));

    /// <summary>General ledger of one account as Excel or PDF (every line, up to 10,000).</summary>
    [HttpGet("general-ledger/export")]
    [Produces("application/pdf", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
    public async Task<IActionResult> ExportGeneralLedger([FromQuery] GeneralLedgerQuery query, [FromQuery] ExportQuery export, CancellationToken cancellationToken) =>
        FileDownloadResult(await _exports.GeneralLedgerAsync(query, export.Format, cancellationToken));

    /// <summary>Profit and loss as Excel or PDF.</summary>
    [HttpGet("profit-loss/export")]
    [Produces("application/pdf", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
    public async Task<IActionResult> ExportProfitLoss([FromQuery] ProfitLossQuery query, [FromQuery] ExportQuery export, CancellationToken cancellationToken) =>
        FileDownloadResult(await _exports.ProfitLossAsync(query, export.Format, cancellationToken));

    /// <summary>Balance sheet as Excel or PDF.</summary>
    [HttpGet("balance-sheet/export")]
    [Produces("application/pdf", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
    public async Task<IActionResult> ExportBalanceSheet([FromQuery] BalanceSheetQuery query, [FromQuery] ExportQuery export, CancellationToken cancellationToken) =>
        FileDownloadResult(await _exports.BalanceSheetAsync(query, export.Format, cancellationToken));

    /// <summary>دفتر الأستاذ العام of one account (a header account includes its sub-accounts), with the running balance.</summary>
    [HttpGet("general-ledger")]
    [ProducesResponseType(typeof(ApiResponse<GeneralLedgerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<GeneralLedgerDto>>> GeneralLedger([FromQuery] GeneralLedgerQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetGeneralLedgerAsync(query, cancellationToken));
    }

    /// <summary>ميزان المراجعة: opening, period and closing columns; level=1..10 rolls the accounts up the tree.</summary>
    [HttpGet("trial-balance")]
    [ProducesResponseType(typeof(ApiResponse<TrialBalanceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TrialBalanceDto>>> TrialBalance([FromQuery] TrialBalanceQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetTrialBalanceAsync(query, cancellationToken));
    }

    /// <summary>قائمة الدخل: revenue, cost of revenue, gross profit, expenses, net profit.</summary>
    [HttpGet("profit-loss")]
    [ProducesResponseType(typeof(ApiResponse<ProfitLossDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ProfitLossDto>>> ProfitLoss([FromQuery] ProfitLossQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetProfitLossAsync(query, cancellationToken));
    }

    /// <summary>الميزانية العمومية: assets = liabilities + equity (+ prior and current year profit).</summary>
    [HttpGet("balance-sheet")]
    [ProducesResponseType(typeof(ApiResponse<BalanceSheetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BalanceSheetDto>>> BalanceSheet([FromQuery] BalanceSheetQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetBalanceSheetAsync(query, cancellationToken));
    }

    /// <summary>ملخص التدفقات النقدية (direct method): sources and uses of the cash accounts by activity.</summary>
    [HttpGet("cash-flow")]
    [ProducesResponseType(typeof(ApiResponse<CashFlowDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CashFlowDto>>> CashFlow([FromQuery] CashFlowQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetCashFlowAsync(query, cancellationToken));
    }

    /// <summary>دفتر اليومية: posted entries with their lines, by date.</summary>
    [HttpGet("reports/journal")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<JournalEntryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<JournalEntryDto>>>> Journal([FromQuery] JournalReportQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetJournalReportAsync(query, cancellationToken));
    }

    [HttpGet("reports/revenue-by-branch")]
    [ProducesResponseType(typeof(ApiResponse<RevenueByBranchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RevenueByBranchDto>>> RevenueByBranch([FromQuery] RevenueByBranchQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetRevenueByBranchAsync(query, cancellationToken));
    }

    [HttpGet("reports/expense-by-category")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseByCategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ExpenseByCategoryDto>>> ExpenseByCategory([FromQuery] ExpenseByCategoryQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetExpenseByCategoryAsync(query, cancellationToken));
    }

    /// <summary>أعمار ذمم الموردين (FIFO on the supplier payable lines).</summary>
    [HttpGet("reports/supplier-aging")]
    [ProducesResponseType(typeof(ApiResponse<PartyAgingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PartyAgingDto>>> SupplierAging([FromQuery] PartyAgingQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetSupplierAgingAsync(query, cancellationToken));
    }

    /// <summary>أعمار ذمم العملاء from the general ledger (FIFO on the receivable lines).</summary>
    [HttpGet("reports/customer-aging")]
    [ProducesResponseType(typeof(ApiResponse<PartyAgingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PartyAgingDto>>> CustomerAging([FromQuery] PartyAgingQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetCustomerAgingAsync(query, cancellationToken));
    }

    /// <summary>Each cashbox balance against its ledger balance.</summary>
    [HttpGet("reports/cashbox-balances")]
    [ProducesResponseType(typeof(ApiResponse<SubledgerReconciliationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SubledgerReconciliationDto>>> CashboxBalances(CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetCashboxBalancesAsync(cancellationToken));
    }

    [HttpGet("reports/bank-balances")]
    [ProducesResponseType(typeof(ApiResponse<BankBalancesDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BankBalancesDto>>> BankBalances(CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetBankBalancesAsync(cancellationToken));
    }

    /// <summary>COD money each driver still holds (driver cash accounts) against the driver custody ledger.</summary>
    [HttpGet("reports/driver-outstanding")]
    [ProducesResponseType(typeof(ApiResponse<SubledgerReconciliationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SubledgerReconciliationDto>>> DriverOutstanding(CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetDriverOutstandingAsync(cancellationToken));
    }

    /// <summary>COD money owed to each sender against the sender COD liability ledger.</summary>
    [HttpGet("reports/sender-payables")]
    [ProducesResponseType(typeof(ApiResponse<SubledgerReconciliationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SubledgerReconciliationDto>>> SenderPayables(CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetSenderPayablesAsync(cancellationToken));
    }

    /// <summary>Every sub-ledger (cashboxes, drivers, senders, customers) against its control account, per currency.</summary>
    [HttpGet("reports/control-accounts")]
    [ProducesResponseType(typeof(ApiResponse<ControlAccountsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ControlAccountsDto>>> ControlAccounts(CancellationToken cancellationToken)
    {
        return OkResponse(await _reports.GetControlAccountsAsync(cancellationToken));
    }
}
