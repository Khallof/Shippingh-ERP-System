using System.Linq.Expressions;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Domain.Accounting;
using ShippingERP.Domain.Entities.Accounting;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Infrastructure.Accounting;

/// <summary>Entity -> DTO projections of the accounting module, translated to SQL by EF Core (step 7).</summary>
public static class AccountingProjections
{
    public static readonly Expression<Func<JournalEntry, JournalEntryListItemDto>> JournalListItem = x => new JournalEntryListItemDto
    {
        Id = x.Id,
        EntryNumber = x.EntryNumber,
        JournalType = x.JournalType,
        Status = x.Status,
        EntryDate = x.EntryDate,
        PeriodName = x.AccountingPeriod.Name,
        BranchId = x.BranchId,
        BranchName = x.Branch.ArabicName,
        CurrencyCode = x.Currency.Code,
        Description = x.Description,
        Reference = x.Reference,
        TotalDebit = x.TotalDebit,
        TotalCredit = x.TotalCredit,
        TotalDebitBase = x.TotalDebitBase,
        IsSystemGenerated = x.IsSystemGenerated,
        SourceType = x.SourceType,
        SourceNumber = x.SourceNumber,
        CreatedAtUtc = x.CreatedAtUtc,
        PostedAtUtc = x.PostedAtUtc
    };

    public static readonly Expression<Func<JournalEntry, JournalEntryDto>> Journal = x => new JournalEntryDto
    {
        Id = x.Id,
        EntryNumber = x.EntryNumber,
        JournalType = x.JournalType,
        Status = x.Status,
        EntryDate = x.EntryDate,
        AccountingPeriodId = x.AccountingPeriodId,
        PeriodName = x.AccountingPeriod.Name,
        PeriodStatus = x.AccountingPeriod.Status,
        BranchId = x.BranchId,
        BranchName = x.Branch.ArabicName,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.Currency.Code,
        ExchangeRate = x.ExchangeRate,
        Description = x.Description,
        Reference = x.Reference,
        TotalDebit = x.TotalDebit,
        TotalCredit = x.TotalCredit,
        TotalDebitBase = x.TotalDebitBase,
        TotalCreditBase = x.TotalCreditBase,
        IsBalanced = x.TotalDebit == x.TotalCredit && x.TotalDebitBase == x.TotalCreditBase,
        IsSystemGenerated = x.IsSystemGenerated,
        SourceType = x.SourceType,
        SourceId = x.SourceId,
        SourceNumber = x.SourceNumber,
        SourceEventType = x.SourceEventType,
        CreatedByUserId = x.CreatedByUserId,
        CreatedAtUtc = x.CreatedAtUtc,
        SubmittedAtUtc = x.SubmittedAtUtc,
        SubmittedByUserId = x.SubmittedByUserId,
        ApprovedAtUtc = x.ApprovedAtUtc,
        ApprovedByUserId = x.ApprovedByUserId,
        RejectedAtUtc = x.RejectedAtUtc,
        RejectedByUserId = x.RejectedByUserId,
        RejectionReason = x.RejectionReason,
        PostedAtUtc = x.PostedAtUtc,
        PostedByUserId = x.PostedByUserId,
        ReversedAtUtc = x.ReversedAtUtc,
        ReversedByUserId = x.ReversedByUserId,
        ReversalOfEntryId = x.ReversalOfEntryId,
        ReversalOfEntryNumber = x.ReversalOfEntry != null ? x.ReversalOfEntry.EntryNumber : null,
        ReversedByEntryId = x.ReversedByEntryId,
        ReversedByEntryNumber = x.ReversedByEntry != null ? x.ReversedByEntry.EntryNumber : null,
        Lines = x.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => new JournalEntryLineDto
            {
                Id = l.Id,
                LineNumber = l.LineNumber,
                AccountId = l.AccountId,
                AccountCode = l.Account.Code,
                AccountName = l.Account.ArabicName,
                Description = l.Description,
                Debit = l.Debit,
                Credit = l.Credit,
                DebitBase = l.DebitBase,
                CreditBase = l.CreditBase,
                BranchId = l.BranchId,
                BranchName = l.Branch.ArabicName,
                AccountRole = l.AccountRole,
                CustomerId = l.CustomerId,
                SupplierId = l.SupplierId,
                DriverId = l.DriverId,
                EmployeeId = l.EmployeeId,
                CashboxId = l.CashboxId,
                BankAccountId = l.BankAccountId,
                ExpenseCategoryId = l.ExpenseCategoryId,
                Dimensions = l.Dimensions
                    .OrderBy(d => d.DimensionType)
                    .Select(d => new JournalLineDimensionDto
                    {
                        DimensionType = d.DimensionType,
                        DimensionId = d.AccountingDimensionId,
                        Code = d.AccountingDimension.Code,
                        Name = d.AccountingDimension.ArabicName
                    })
                    .ToList()
            })
            .ToList()
    };

    public static readonly Expression<Func<Account, AccountDto>> AccountItem = x => new AccountDto
    {
        Id = x.Id,
        Code = x.Code,
        ArabicName = x.ArabicName,
        EnglishName = x.EnglishName,
        AccountType = x.AccountType,
        NormalBalance = x.NormalBalance,
        AccountCategoryId = x.AccountCategoryId,
        AccountCategoryName = x.AccountCategory != null ? x.AccountCategory.ArabicName : null,
        ParentAccountId = x.ParentAccountId,
        ParentAccountCode = x.ParentAccount != null ? x.ParentAccount.Code : null,
        ParentAccountName = x.ParentAccount != null ? x.ParentAccount.ArabicName : null,
        Level = x.Level,
        IsPostable = x.IsPostable,
        IsSystem = x.IsSystem,
        IsCashAccount = x.IsCashAccount,
        AllowManualPosting = x.AllowManualPosting,
        RequiresCostCenter = x.RequiresCostCenter,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.Currency != null ? x.Currency.Code : null,
        Description = x.Description,
        IsActive = x.IsActive,
        CreatedAtUtc = x.CreatedAtUtc
    };

    public static readonly Expression<Func<AccountingPostingEvent, PostingEventDto>> PostingEvent = x => new PostingEventDto
    {
        Id = x.Id,
        EventId = x.EventId,
        OutboxMessageId = x.OutboxMessageId,
        EventType = x.EventType,
        SourceType = x.SourceType,
        SourceId = x.SourceId,
        SourceNumber = x.SourceNumber,
        SourceKey = x.SourceKey,
        OccurredAtUtc = x.OccurredAtUtc,
        PostingDate = x.PostingDate,
        Status = x.Status,
        JournalEntryId = x.JournalEntryId,
        JournalEntryNumber = x.JournalEntry != null ? x.JournalEntry.EntryNumber : null,
        Attempts = x.Attempts,
        LastError = x.LastError,
        ProcessedAtUtc = x.ProcessedAtUtc
    };

    // ------------------------------------------------------------------ Arabic names (filled after the query)

    public static JournalEntryListItemDto WithNames(this JournalEntryListItemDto dto) => new()
    {
        Id = dto.Id,
        EntryNumber = dto.EntryNumber,
        JournalType = dto.JournalType,
        JournalTypeName = AccountingNames.Arabic(dto.JournalType),
        Status = dto.Status,
        StatusName = AccountingNames.Arabic(dto.Status),
        EntryDate = dto.EntryDate,
        PeriodName = dto.PeriodName,
        BranchId = dto.BranchId,
        BranchName = dto.BranchName,
        CurrencyCode = dto.CurrencyCode,
        Description = dto.Description,
        Reference = dto.Reference,
        TotalDebit = dto.TotalDebit,
        TotalCredit = dto.TotalCredit,
        TotalDebitBase = dto.TotalDebitBase,
        IsSystemGenerated = dto.IsSystemGenerated,
        SourceType = dto.SourceType,
        SourceNumber = dto.SourceNumber,
        CreatedAtUtc = dto.CreatedAtUtc,
        PostedAtUtc = dto.PostedAtUtc
    };

    public static JournalEntryDto WithNames(this JournalEntryDto dto) => new()
    {
        Id = dto.Id,
        EntryNumber = dto.EntryNumber,
        JournalType = dto.JournalType,
        JournalTypeName = AccountingNames.Arabic(dto.JournalType),
        Status = dto.Status,
        StatusName = AccountingNames.Arabic(dto.Status),
        EntryDate = dto.EntryDate,
        AccountingPeriodId = dto.AccountingPeriodId,
        PeriodName = dto.PeriodName,
        PeriodStatus = dto.PeriodStatus,
        BranchId = dto.BranchId,
        BranchName = dto.BranchName,
        CurrencyId = dto.CurrencyId,
        CurrencyCode = dto.CurrencyCode,
        ExchangeRate = dto.ExchangeRate,
        Description = dto.Description,
        Reference = dto.Reference,
        TotalDebit = dto.TotalDebit,
        TotalCredit = dto.TotalCredit,
        TotalDebitBase = dto.TotalDebitBase,
        TotalCreditBase = dto.TotalCreditBase,
        IsBalanced = dto.IsBalanced,
        IsSystemGenerated = dto.IsSystemGenerated,
        SourceType = dto.SourceType,
        SourceId = dto.SourceId,
        SourceNumber = dto.SourceNumber,
        SourceEventType = dto.SourceEventType,
        CreatedByUserId = dto.CreatedByUserId,
        CreatedAtUtc = dto.CreatedAtUtc,
        SubmittedAtUtc = dto.SubmittedAtUtc,
        SubmittedByUserId = dto.SubmittedByUserId,
        ApprovedAtUtc = dto.ApprovedAtUtc,
        ApprovedByUserId = dto.ApprovedByUserId,
        RejectedAtUtc = dto.RejectedAtUtc,
        RejectedByUserId = dto.RejectedByUserId,
        RejectionReason = dto.RejectionReason,
        PostedAtUtc = dto.PostedAtUtc,
        PostedByUserId = dto.PostedByUserId,
        ReversedAtUtc = dto.ReversedAtUtc,
        ReversedByUserId = dto.ReversedByUserId,
        ReversalOfEntryId = dto.ReversalOfEntryId,
        ReversalOfEntryNumber = dto.ReversalOfEntryNumber,
        ReversedByEntryId = dto.ReversedByEntryId,
        ReversedByEntryNumber = dto.ReversedByEntryNumber,
        Lines = dto.Lines
            .Select(l => new JournalEntryLineDto
            {
                Id = l.Id,
                LineNumber = l.LineNumber,
                AccountId = l.AccountId,
                AccountCode = l.AccountCode,
                AccountName = l.AccountName,
                Description = l.Description,
                Debit = l.Debit,
                Credit = l.Credit,
                DebitBase = l.DebitBase,
                CreditBase = l.CreditBase,
                BranchId = l.BranchId,
                BranchName = l.BranchName,
                AccountRole = l.AccountRole,
                CustomerId = l.CustomerId,
                SupplierId = l.SupplierId,
                DriverId = l.DriverId,
                EmployeeId = l.EmployeeId,
                CashboxId = l.CashboxId,
                BankAccountId = l.BankAccountId,
                ExpenseCategoryId = l.ExpenseCategoryId,
                Dimensions = l.Dimensions
                    .Select(d => new JournalLineDimensionDto
                    {
                        DimensionType = d.DimensionType,
                        DimensionTypeName = AccountingNames.Arabic(d.DimensionType),
                        DimensionId = d.DimensionId,
                        Code = d.Code,
                        Name = d.Name
                    })
                    .ToList()
            })
            .ToList()
    };

    public static AccountDto WithNames(this AccountDto dto) => new()
    {
        Id = dto.Id,
        Code = dto.Code,
        ArabicName = dto.ArabicName,
        EnglishName = dto.EnglishName,
        AccountType = dto.AccountType,
        AccountTypeName = AccountingNames.Arabic(dto.AccountType),
        NormalBalance = dto.NormalBalance,
        NormalBalanceName = AccountingNames.Arabic(dto.NormalBalance),
        AccountCategoryId = dto.AccountCategoryId,
        AccountCategoryName = dto.AccountCategoryName,
        ParentAccountId = dto.ParentAccountId,
        ParentAccountCode = dto.ParentAccountCode,
        ParentAccountName = dto.ParentAccountName,
        Level = dto.Level,
        IsPostable = dto.IsPostable,
        IsSystem = dto.IsSystem,
        IsCashAccount = dto.IsCashAccount,
        AllowManualPosting = dto.AllowManualPosting,
        RequiresCostCenter = dto.RequiresCostCenter,
        CurrencyId = dto.CurrencyId,
        CurrencyCode = dto.CurrencyCode,
        Description = dto.Description,
        IsActive = dto.IsActive,
        CreatedAtUtc = dto.CreatedAtUtc
    };

    public static PostingEventDto WithNames(this PostingEventDto dto) => new()
    {
        Id = dto.Id,
        EventId = dto.EventId,
        OutboxMessageId = dto.OutboxMessageId,
        EventType = dto.EventType,
        SourceType = dto.SourceType,
        SourceId = dto.SourceId,
        SourceNumber = dto.SourceNumber,
        SourceKey = dto.SourceKey,
        OccurredAtUtc = dto.OccurredAtUtc,
        PostingDate = dto.PostingDate,
        Status = dto.Status,
        StatusName = AccountingNames.Arabic(dto.Status),
        JournalEntryId = dto.JournalEntryId,
        JournalEntryNumber = dto.JournalEntryNumber,
        Attempts = dto.Attempts,
        LastError = dto.LastError,
        ProcessedAtUtc = dto.ProcessedAtUtc
    };

    /// <summary>Statuses whose lines are in the general ledger (used in the report queries).</summary>
    public static readonly JournalEntryStatus[] LedgerStatuses = { JournalEntryStatus.Posted, JournalEntryStatus.Reversed };
}
