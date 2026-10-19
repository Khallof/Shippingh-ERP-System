using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Accounting;

// Financial reports (step 7). All amounts are in the BASE currency unless a field says otherwise, and only
// posted entries count (Posted and Reversed: a reversed entry and its reversal both stay in the ledger).
// Dates are accounting dates; ToDate is inclusive. Users without Branches.ViewAll always get their own branch.

/// <summary>GET /api/v1/accounting/general-ledger.</summary>
public sealed class GeneralLedgerQuery : PagedQuery
{
    /// <summary>Required. A header account includes all its sub-accounts.</summary>
    public long AccountId { get; set; }

    /// <summary>Default: the first day of the current fiscal year.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Default: today.</summary>
    public DateTime? ToDate { get; set; }

    public long? BranchId { get; set; }

    public long? CostCenterId { get; set; }
}

public sealed class GeneralLedgerLineDto
{
    public long JournalEntryId { get; init; }
    public string EntryNumber { get; init; } = string.Empty;
    public DateTime EntryDate { get; init; }
    public JournalType JournalType { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? LineDescription { get; init; }
    public string? Reference { get; init; }
    public string? SourceNumber { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string BranchName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;

    /// <summary>Amount in the entry currency (useful for foreign-currency accounts).</summary>
    public decimal AmountInCurrency { get; init; }

    public decimal Debit { get; init; }
    public decimal Credit { get; init; }

    /// <summary>Running balance (debit − credit) after this line.</summary>
    public decimal RunningBalance { get; init; }
}

public sealed class GeneralLedgerDto
{
    public long AccountId { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string AccountName { get; init; } = string.Empty;
    public AccountType AccountType { get; init; }
    public NormalBalance NormalBalance { get; init; }
    public bool IncludesSubAccounts { get; init; }
    public DateTime FromDate { get; init; }
    public DateTime ToDate { get; init; }
    public string BaseCurrencyCode { get; init; } = string.Empty;

    /// <summary>Debit − credit before FromDate (profit and loss accounts: since the start of the fiscal year).</summary>
    public decimal OpeningBalance { get; init; }
    public decimal PeriodDebit { get; init; }
    public decimal PeriodCredit { get; init; }
    public decimal ClosingBalance { get; init; }

    public PagedResult<GeneralLedgerLineDto> Lines { get; init; } = new(Array.Empty<GeneralLedgerLineDto>(), 1, 20, 0);
}

/// <summary>GET /api/v1/accounting/trial-balance.</summary>
public sealed class TrialBalanceQuery
{
    /// <summary>Default: the first day of the current fiscal year.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Default: today.</summary>
    public DateTime? ToDate { get; set; }

    public long? BranchId { get; set; }

    public long? CostCenterId { get; set; }

    /// <summary>Also list accounts without any balance or movement.</summary>
    public bool IncludeZeroBalances { get; set; }

    /// <summary>Roll the postable accounts up to this tree level (1 = main groups). Default: postable accounts.</summary>
    public int? Level { get; set; }
}

public sealed class TrialBalanceRowDto
{
    public long AccountId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public AccountType AccountType { get; init; }
    public int Level { get; init; }
    public bool IsPostable { get; init; }
    public decimal OpeningDebit { get; init; }
    public decimal OpeningCredit { get; init; }
    public decimal PeriodDebit { get; init; }
    public decimal PeriodCredit { get; init; }
    public decimal ClosingDebit { get; init; }
    public decimal ClosingCredit { get; init; }
}

public sealed class TrialBalanceDto
{
    public DateTime FromDate { get; init; }
    public DateTime ToDate { get; init; }
    public string BaseCurrencyCode { get; init; } = string.Empty;
    public IReadOnlyList<TrialBalanceRowDto> Rows { get; init; } = Array.Empty<TrialBalanceRowDto>();
    public decimal TotalOpeningDebit { get; init; }
    public decimal TotalOpeningCredit { get; init; }
    public decimal TotalPeriodDebit { get; init; }
    public decimal TotalPeriodCredit { get; init; }
    public decimal TotalClosingDebit { get; init; }
    public decimal TotalClosingCredit { get; init; }

    /// <summary>Opening, period and closing columns all balanced.</summary>
    public bool IsBalanced { get; init; }
}

/// <summary>GET /api/v1/accounting/profit-loss.</summary>
public sealed class ProfitLossQuery
{
    /// <summary>Default: the first day of the current fiscal year.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Default: today.</summary>
    public DateTime? ToDate { get; set; }

    public long? BranchId { get; set; }

    public long? CostCenterId { get; set; }
}

public sealed class StatementAccountDto
{
    public long AccountId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}

/// <summary>A category of a financial statement with its accounts (Amount in the natural sign of the section).</summary>
public sealed class StatementGroupDto
{
    public long? CategoryId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public IReadOnlyList<StatementAccountDto> Accounts { get; init; } = Array.Empty<StatementAccountDto>();
}

public sealed class ProfitLossDto
{
    public DateTime FromDate { get; init; }
    public DateTime ToDate { get; init; }
    public string BaseCurrencyCode { get; init; } = string.Empty;
    public IReadOnlyList<StatementGroupDto> Revenue { get; init; } = Array.Empty<StatementGroupDto>();
    public decimal TotalRevenue { get; init; }
    public IReadOnlyList<StatementGroupDto> CostOfRevenue { get; init; } = Array.Empty<StatementGroupDto>();
    public decimal TotalCostOfRevenue { get; init; }
    public decimal GrossProfit { get; init; }
    public IReadOnlyList<StatementGroupDto> Expenses { get; init; } = Array.Empty<StatementGroupDto>();
    public decimal TotalExpenses { get; init; }
    public decimal NetProfit { get; init; }

    /// <summary>Net profit / revenue × 100 (null when there is no revenue).</summary>
    public decimal? NetProfitMarginPercent { get; init; }
}

/// <summary>GET /api/v1/accounting/balance-sheet.</summary>
public sealed class BalanceSheetQuery
{
    /// <summary>Default: today.</summary>
    public DateTime? AsOfDate { get; set; }

    public long? BranchId { get; set; }
}

public sealed class BalanceSheetDto
{
    public DateTime AsOfDate { get; init; }
    public string BaseCurrencyCode { get; init; } = string.Empty;
    public IReadOnlyList<StatementGroupDto> Assets { get; init; } = Array.Empty<StatementGroupDto>();
    public decimal TotalAssets { get; init; }
    public IReadOnlyList<StatementGroupDto> Liabilities { get; init; } = Array.Empty<StatementGroupDto>();
    public decimal TotalLiabilities { get; init; }
    public IReadOnlyList<StatementGroupDto> Equity { get; init; } = Array.Empty<StatementGroupDto>();

    /// <summary>Profit of the fiscal years before the current one not yet closed into retained earnings.</summary>
    public decimal PriorYearsProfit { get; init; }

    /// <summary>Profit of the current fiscal year up to AsOfDate.</summary>
    public decimal CurrentYearProfit { get; init; }

    public decimal TotalEquity { get; init; }
    public decimal TotalLiabilitiesAndEquity { get; init; }

    /// <summary>
    /// For one branch only: entries between branches (inter-branch cash transfers) balance at company level, not per
    /// branch. This is the amount that makes the branch statement balance.
    /// </summary>
    public decimal InterBranchDifference { get; init; }

    public bool IsBalanced { get; init; }
}

/// <summary>GET /api/v1/accounting/cash-flow (direct method, from the cash accounts).</summary>
public sealed class CashFlowQuery
{
    /// <summary>Default: the first day of the current fiscal year.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Default: today.</summary>
    public DateTime? ToDate { get; set; }

    public long? BranchId { get; set; }
}

public sealed class CashFlowLineDto
{
    public long AccountId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public decimal Inflow { get; init; }
    public decimal Outflow { get; init; }
    public decimal Net { get; init; }
}

public sealed class CashFlowSectionDto
{
    public CashFlowActivity Activity { get; init; }
    public string ActivityName { get; init; } = string.Empty;
    public IReadOnlyList<CashFlowLineDto> Lines { get; init; } = Array.Empty<CashFlowLineDto>();
    public decimal TotalInflow { get; init; }
    public decimal TotalOutflow { get; init; }
    public decimal Net { get; init; }
}

public sealed class CashAccountBalanceDto
{
    public long AccountId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public decimal Opening { get; init; }
    public decimal Closing { get; init; }
}

public sealed class CashFlowDto
{
    public DateTime FromDate { get; init; }
    public DateTime ToDate { get; init; }
    public string BaseCurrencyCode { get; init; } = string.Empty;
    public decimal OpeningCash { get; init; }
    public IReadOnlyList<CashFlowSectionDto> Sections { get; init; } = Array.Empty<CashFlowSectionDto>();
    public decimal NetChange { get; init; }
    public decimal ClosingCash { get; init; }
    public IReadOnlyList<CashAccountBalanceDto> CashAccounts { get; init; } = Array.Empty<CashAccountBalanceDto>();

    /// <summary>Opening + net change equals the closing balance of the cash accounts.</summary>
    public bool IsReconciled { get; init; }
}

/// <summary>GET /api/v1/accounting/reports/journal: posted entries with their lines.</summary>
public sealed class JournalReportQuery : PagedQuery
{
    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public JournalType? JournalType { get; set; }

    public long? BranchId { get; set; }
}

/// <summary>GET /api/v1/accounting/reports/revenue-by-branch.</summary>
public sealed class RevenueByBranchQuery
{
    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }
}

public sealed class RevenueByBranchRowDto
{
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public decimal ShippingRevenue { get; init; }
    public decimal CodCommissionRevenue { get; init; }
    public decimal AdditionalServiceRevenue { get; init; }
    public decimal OtherRevenue { get; init; }
    public decimal TotalRevenue { get; init; }
    public decimal CostOfRevenue { get; init; }
    public decimal Expenses { get; init; }
    public decimal NetProfit { get; init; }
}

public sealed class RevenueByBranchDto
{
    public DateTime FromDate { get; init; }
    public DateTime ToDate { get; init; }
    public string BaseCurrencyCode { get; init; } = string.Empty;
    public IReadOnlyList<RevenueByBranchRowDto> Rows { get; init; } = Array.Empty<RevenueByBranchRowDto>();
    public RevenueByBranchRowDto Totals { get; init; } = new();
}

/// <summary>GET /api/v1/accounting/reports/expense-by-category.</summary>
public sealed class ExpenseByCategoryQuery
{
    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public long? BranchId { get; set; }

    public long? CostCenterId { get; set; }
}

public sealed class ExpenseAccountRowDto
{
    public long AccountId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public AccountType AccountType { get; init; }
    public decimal Amount { get; init; }
    public decimal Percent { get; init; }
}

public sealed class ExpenseCategoryRowDto
{
    /// <summary>Operational expense category of the cash payment voucher (null = without category, e.g. manual entries).</summary>
    public long? ExpenseCategoryId { get; init; }
    public string ArabicName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal Percent { get; init; }
}

public sealed class ExpenseByCategoryDto
{
    public DateTime FromDate { get; init; }
    public DateTime ToDate { get; init; }
    public string BaseCurrencyCode { get; init; } = string.Empty;
    public IReadOnlyList<ExpenseAccountRowDto> ByAccount { get; init; } = Array.Empty<ExpenseAccountRowDto>();
    public IReadOnlyList<ExpenseCategoryRowDto> ByExpenseCategory { get; init; } = Array.Empty<ExpenseCategoryRowDto>();
    public decimal Total { get; init; }
}

/// <summary>GET /api/v1/accounting/reports/supplier-aging and customer-aging (FIFO on the ledger lines).</summary>
public sealed class PartyAgingQuery
{
    /// <summary>Default: today.</summary>
    public DateTime? AsOfDate { get; set; }

    public long? BranchId { get; set; }
}

public sealed class PartyAgingRowDto
{
    public long PartyId { get; init; }
    public string PartyCode { get; init; } = string.Empty;
    public string PartyName { get; init; } = string.Empty;
    public decimal Days0To30 { get; init; }
    public decimal Days31To60 { get; init; }
    public decimal Days61To90 { get; init; }
    public decimal Over90Days { get; init; }
    public decimal TotalOpen { get; init; }

    /// <summary>Payments not applied to any open item (advances / credit balance).</summary>
    public decimal Unapplied { get; init; }

    public decimal NetBalance { get; init; }
}

public sealed class PartyAgingDto
{
    public DateTime AsOfDate { get; init; }
    public string PartyType { get; init; } = string.Empty;
    public string BaseCurrencyCode { get; init; } = string.Empty;
    public IReadOnlyList<PartyAgingRowDto> Rows { get; init; } = Array.Empty<PartyAgingRowDto>();
    public PartyAgingRowDto Totals { get; init; } = new();
}

/// <summary>
/// A sub-ledger balance compared with the general ledger (cashbox, driver custody, sender COD money, customer
/// receivables). Amounts in the row currency.
/// </summary>
public sealed class SubledgerBalanceRowDto
{
    public long ReferenceId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? BranchName { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal SubledgerBalance { get; init; }
    public decimal LedgerBalance { get; init; }
    public decimal Difference { get; init; }

    /// <summary>Extra figure of the report (COD count with the driver, unsettled net for a sender...).</summary>
    public decimal? Extra { get; init; }
}

public sealed class CurrencyTotalDto
{
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal SubledgerBalance { get; init; }
    public decimal LedgerBalance { get; init; }
    public decimal Difference { get; init; }
}

public sealed class SubledgerReconciliationDto
{
    public string Title { get; init; } = string.Empty;
    public DateTime GeneratedAtUtc { get; init; }
    public IReadOnlyList<SubledgerBalanceRowDto> Rows { get; init; } = Array.Empty<SubledgerBalanceRowDto>();
    public IReadOnlyList<CurrencyTotalDto> Totals { get; init; } = Array.Empty<CurrencyTotalDto>();

    /// <summary>Events not posted yet: the ledger lags behind the sub-ledger until they are.</summary>
    public int PendingPostingEvents { get; init; }

    public int FailedPostingEvents { get; init; }

    /// <summary>No difference on any row.</summary>
    public bool IsReconciled { get; init; }
}

public sealed class BankBalanceRowDto
{
    public long BankAccountId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string BankName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public string AccountCode { get; init; } = string.Empty;
    public decimal LedgerBalance { get; init; }
    public decimal LedgerBalanceBase { get; init; }
    public DateTime? LastReconciledDate { get; init; }
    public decimal? LastStatementBalance { get; init; }
    public int UnreconciledStatementLines { get; init; }
}

public sealed class BankBalancesDto
{
    public DateTime AsOfDate { get; init; }
    public string BaseCurrencyCode { get; init; } = string.Empty;
    public IReadOnlyList<BankBalanceRowDto> Rows { get; init; } = Array.Empty<BankBalanceRowDto>();
    public decimal TotalBase { get; init; }
}

/// <summary>GET /api/v1/accounting/reports/control-accounts: every sub-ledger against its control account.</summary>
public sealed class ControlAccountRowDto
{
    public string Control { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal SubledgerBalance { get; init; }
    public decimal LedgerBalance { get; init; }
    public decimal Difference { get; init; }
}

public sealed class ControlAccountsDto
{
    public DateTime GeneratedAtUtc { get; init; }
    public IReadOnlyList<ControlAccountRowDto> Rows { get; init; } = Array.Empty<ControlAccountRowDto>();
    public int PendingPostingEvents { get; init; }
    public int FailedPostingEvents { get; init; }
    public bool IsReconciled { get; init; }
}
