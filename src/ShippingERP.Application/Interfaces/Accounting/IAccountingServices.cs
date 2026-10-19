using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;

namespace ShippingERP.Application.Interfaces.Accounting;

/// <summary>Chart of accounts (دليل الحسابات), account categories and analytical dimensions (step 7).</summary>
public interface IChartOfAccountsService
{
    Task<PagedResult<AccountDto>> GetAccountsAsync(AccountListQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountTreeNodeDto>> GetTreeAsync(AccountTreeQuery query, CancellationToken cancellationToken);

    Task<AccountDto> GetAccountAsync(long id, CancellationToken cancellationToken);

    Task<AccountDto> CreateAccountAsync(SaveAccountRequest request, CancellationToken cancellationToken);

    Task<AccountDto> UpdateAccountAsync(long id, SaveAccountRequest request, CancellationToken cancellationToken);

    /// <summary>Soft delete; refused for system accounts and accounts with children, journal lines or posting rules.</summary>
    Task DeleteAccountAsync(long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken);

    Task<AccountCategoryDto> CreateCategoryAsync(SaveAccountCategoryRequest request, CancellationToken cancellationToken);

    Task<AccountCategoryDto> UpdateCategoryAsync(long id, SaveAccountCategoryRequest request, CancellationToken cancellationToken);

    Task DeleteCategoryAsync(long id, CancellationToken cancellationToken);

    Task<PagedResult<AccountingDimensionDto>> GetDimensionsAsync(AccountingDimensionListQuery query, CancellationToken cancellationToken);

    Task<AccountingDimensionDto> GetDimensionAsync(long id, CancellationToken cancellationToken);

    Task<AccountingDimensionDto> CreateDimensionAsync(SaveAccountingDimensionRequest request, CancellationToken cancellationToken);

    Task<AccountingDimensionDto> UpdateDimensionAsync(long id, SaveAccountingDimensionRequest request, CancellationToken cancellationToken);

    Task DeleteDimensionAsync(long id, CancellationToken cancellationToken);
}

/// <summary>Fiscal years and accounting periods: creation, closing and reopening (step 7).</summary>
public interface IFiscalPeriodService
{
    Task<IReadOnlyList<FiscalYearDto>> GetFiscalYearsAsync(CancellationToken cancellationToken);

    Task<FiscalYearDto> GetFiscalYearAsync(long id, CancellationToken cancellationToken);

    /// <summary>Creates the year and its monthly periods (no overlap with another year).</summary>
    Task<FiscalYearDto> CreateFiscalYearAsync(CreateFiscalYearRequest request, CancellationToken cancellationToken);

    Task<FiscalYearDto> UpdateFiscalYearAsync(long id, UpdateFiscalYearRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes a year (and its periods) that has no journal entry.</summary>
    Task DeleteFiscalYearAsync(long id, CancellationToken cancellationToken);

    /// <summary>Closes a year whose periods are all closed.</summary>
    Task<FiscalYearDto> CloseFiscalYearAsync(long id, CancellationToken cancellationToken);

    Task<PagedResult<AccountingPeriodDto>> GetPeriodsAsync(AccountingPeriodListQuery query, CancellationToken cancellationToken);

    Task<AccountingPeriodDto> GetPeriodAsync(long id, CancellationToken cancellationToken);

    Task<AccountingPeriodDto> UpdatePeriodAsync(long id, UpdateAccountingPeriodRequest request, CancellationToken cancellationToken);

    Task<PeriodCloseCheckDto> GetCloseCheckAsync(long id, CancellationToken cancellationToken);

    /// <summary>Closes the period: 422 while unposted entries or unposted operational events remain.</summary>
    Task<AccountingPeriodDto> ClosePeriodAsync(long id, CloseAccountingPeriodRequest request, CancellationToken cancellationToken);

    Task<AccountingPeriodDto> ReopenPeriodAsync(long id, ReopenAccountingPeriodRequest request, CancellationToken cancellationToken);
}

/// <summary>Manual journal entries and their workflow; read access to every entry (step 7).</summary>
public interface IJournalEntryService
{
    Task<PagedResult<JournalEntryListItemDto>> GetPagedAsync(JournalEntryListQuery query, CancellationToken cancellationToken);

    Task<JournalEntryDto> GetByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>Creates a Draft (or Submitted) entry. The same Idempotency-Key returns the first entry.</summary>
    Task<JournalCreationResult> CreateAsync(SaveJournalEntryRequest request, string? idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Replaces a Draft / Rejected entry (it becomes Draft again unless Submit = true).</summary>
    Task<JournalEntryDto> UpdateAsync(long id, SaveJournalEntryRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(long id, CancellationToken cancellationToken);

    Task<JournalEntryDto> SubmitAsync(long id, CancellationToken cancellationToken);

    Task<JournalEntryDto> ApproveAsync(long id, JournalDecisionRequest request, CancellationToken cancellationToken);

    Task<JournalEntryDto> RejectAsync(long id, JournalReasonRequest request, CancellationToken cancellationToken);

    Task<JournalEntryDto> PostAsync(long id, CancellationToken cancellationToken);

    /// <summary>Creates and posts the reversal entry; the original becomes Reversed. Returns the reversal entry.</summary>
    Task<JournalEntryDto> ReverseAsync(long id, ReverseJournalEntryRequest request, CancellationToken cancellationToken);
}

/// <summary>Automatic posting rules (role -> account) and the processing of the operational events (step 7).</summary>
public interface IPostingRuleService
{
    Task<PagedResult<PostingRuleDto>> GetPagedAsync(PostingRuleListQuery query, CancellationToken cancellationToken);

    Task<PostingRuleDto> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<PostingRuleDto> CreateAsync(SavePostingRuleRequest request, CancellationToken cancellationToken);

    Task<PostingRuleDto> UpdateAsync(long id, SavePostingRuleRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(long id, CancellationToken cancellationToken);

    IReadOnlyList<AccountRoleDto> GetRoles();

    Task<ResolvedPostingAccountDto> ResolveAsync(ResolvePostingAccountQuery query, CancellationToken cancellationToken);
}

public interface IAccountingPostingService
{
    Task<PagedResult<PostingEventDto>> GetEventsAsync(PostingEventListQuery query, CancellationToken cancellationToken);

    Task<PostingEventDto> GetEventAsync(long id, CancellationToken cancellationToken);

    Task<PostingSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);

    /// <summary>Processes up to MaxCount waiting outbox messages now (the background job does the same every few seconds).</summary>
    Task<ProcessPostingsResultDto> ProcessPendingAsync(int maxCount, CancellationToken cancellationToken);

    /// <summary>Processes a failed event again, optionally on another (open) accounting date.</summary>
    Task<PostingEventDto> RetryAsync(long postingEventId, RetryPostingEventRequest request, CancellationToken cancellationToken);
}

/// <summary>Bank statement lines and bank reconciliations (step 7).</summary>
public interface IBankReconciliationService
{
    Task<PagedResult<BankTransactionDto>> GetTransactionsAsync(BankTransactionListQuery query, CancellationToken cancellationToken);

    Task<PagedResult<BankReconciliationDto>> GetPagedAsync(BankReconciliationListQuery query, CancellationToken cancellationToken);

    Task<BankReconciliationDto> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<BankReconciliationDto> CreateAsync(CreateBankReconciliationRequest request, CancellationToken cancellationToken);

    /// <summary>Recalculates a Draft reconciliation and completes it when the difference is 0.</summary>
    Task<BankReconciliationDto> CompleteAsync(long id, CompleteBankReconciliationRequest request, CancellationToken cancellationToken);
}

/// <summary>Financial statements and ledger reports (step 7).</summary>
public interface IFinancialReportService
{
    Task<GeneralLedgerDto> GetGeneralLedgerAsync(GeneralLedgerQuery query, CancellationToken cancellationToken);

    Task<TrialBalanceDto> GetTrialBalanceAsync(TrialBalanceQuery query, CancellationToken cancellationToken);

    Task<ProfitLossDto> GetProfitLossAsync(ProfitLossQuery query, CancellationToken cancellationToken);

    Task<BalanceSheetDto> GetBalanceSheetAsync(BalanceSheetQuery query, CancellationToken cancellationToken);

    Task<CashFlowDto> GetCashFlowAsync(CashFlowQuery query, CancellationToken cancellationToken);

    Task<PagedResult<JournalEntryDto>> GetJournalReportAsync(JournalReportQuery query, CancellationToken cancellationToken);

    Task<RevenueByBranchDto> GetRevenueByBranchAsync(RevenueByBranchQuery query, CancellationToken cancellationToken);

    Task<ExpenseByCategoryDto> GetExpenseByCategoryAsync(ExpenseByCategoryQuery query, CancellationToken cancellationToken);

    Task<PartyAgingDto> GetSupplierAgingAsync(PartyAgingQuery query, CancellationToken cancellationToken);

    Task<PartyAgingDto> GetCustomerAgingAsync(PartyAgingQuery query, CancellationToken cancellationToken);

    Task<SubledgerReconciliationDto> GetCashboxBalancesAsync(CancellationToken cancellationToken);

    Task<BankBalancesDto> GetBankBalancesAsync(CancellationToken cancellationToken);

    Task<SubledgerReconciliationDto> GetDriverOutstandingAsync(CancellationToken cancellationToken);

    Task<SubledgerReconciliationDto> GetSenderPayablesAsync(CancellationToken cancellationToken);

    Task<ControlAccountsDto> GetControlAccountsAsync(CancellationToken cancellationToken);
}
