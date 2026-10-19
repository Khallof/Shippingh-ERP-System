using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Accounting;

public sealed class JournalLineDimensionRequest
{
    public DimensionType DimensionType { get; set; }

    public long DimensionId { get; set; }
}

/// <summary>One line of a manual journal entry: exactly one of Debit / Credit is greater than zero.</summary>
public sealed class JournalLineRequest
{
    /// <summary>A postable, active account that allows manual posting.</summary>
    public long AccountId { get; set; }

    public string? Description { get; set; }

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    /// <summary>Default: the entry branch.</summary>
    public long? BranchId { get; set; }

    /// <summary>Shortcut for a CostCenter dimension.</summary>
    public long? CostCenterId { get; set; }

    /// <summary>Other dimensions (project, vehicle, department); one per type.</summary>
    public List<JournalLineDimensionRequest> Dimensions { get; set; } = new();

    public long? CustomerId { get; set; }

    public long? SupplierId { get; set; }

    public long? DriverId { get; set; }

    public long? EmployeeId { get; set; }

    public long? CashboxId { get; set; }

    public long? BankAccountId { get; set; }

    public long? ExpenseCategoryId { get; set; }
}

/// <summary>
/// Body of POST /api/v1/accounting/journal-entries (and PUT for Draft / Rejected entries).
/// The entry is saved as Draft; Submit = true submits it for approval at once.
/// </summary>
public sealed class SaveJournalEntryRequest
{
    /// <summary>Accounting date (only the date part is used).</summary>
    public DateTime EntryDate { get; set; }

    /// <summary>General (default), Opening or Adjustment. The other journals are automatic.</summary>
    public JournalType JournalType { get; set; } = JournalType.General;

    /// <summary>Default: the base currency.</summary>
    public long? CurrencyId { get; set; }

    /// <summary>Default: the latest exchange rate of the currency at the entry date (1 for the base currency).</summary>
    public decimal? ExchangeRate { get; set; }

    /// <summary>Default: the caller's branch, else the head office.</summary>
    public long? BranchId { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? Reference { get; set; }

    public List<JournalLineRequest> Lines { get; set; } = new();

    public bool Submit { get; set; }
}

public sealed class JournalDecisionRequest
{
    public string? Notes { get; set; }
}

public sealed class JournalReasonRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Body of POST /api/v1/accounting/journal-entries/{id}/reverse.</summary>
public sealed class ReverseJournalEntryRequest
{
    /// <summary>Date of the reversal entry (default: today). Its period must be open.</summary>
    public DateTime? ReversalDate { get; set; }

    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// GET /api/v1/accounting/journal-entries. Search: entry number, description, reference, source number.
/// SortBy: entryDate (default, newest first), entryNumber, totalDebit.
/// </summary>
public sealed class JournalEntryListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "entryDate", "entryNumber", "totalDebit" };

    public JournalEntryStatus? Status { get; set; }

    public JournalType? JournalType { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public long? BranchId { get; set; }

    /// <summary>Entries with at least one line on this account.</summary>
    public long? AccountId { get; set; }

    public string? SourceType { get; set; }

    public bool? IsSystemGenerated { get; set; }

    public long? AccountingPeriodId { get; set; }
}

public sealed class JournalEntryListItemDto
{
    public long Id { get; init; }
    public string EntryNumber { get; init; } = string.Empty;
    public JournalType JournalType { get; init; }
    public string JournalTypeName { get; init; } = string.Empty;
    public JournalEntryStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTime EntryDate { get; init; }
    public string PeriodName { get; init; } = string.Empty;
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Reference { get; init; }
    public decimal TotalDebit { get; init; }
    public decimal TotalCredit { get; init; }
    public decimal TotalDebitBase { get; init; }
    public bool IsSystemGenerated { get; init; }
    public string? SourceType { get; init; }
    public string? SourceNumber { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? PostedAtUtc { get; init; }
}

public sealed class JournalLineDimensionDto
{
    public DimensionType DimensionType { get; init; }
    public string DimensionTypeName { get; init; } = string.Empty;
    public long DimensionId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class JournalEntryLineDto
{
    public long Id { get; init; }
    public int LineNumber { get; init; }
    public long AccountId { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string AccountName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
    public decimal DebitBase { get; init; }
    public decimal CreditBase { get; init; }
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public string? AccountRole { get; init; }
    public long? CustomerId { get; init; }
    public long? SupplierId { get; init; }
    public long? DriverId { get; init; }
    public long? EmployeeId { get; init; }
    public long? CashboxId { get; init; }
    public long? BankAccountId { get; init; }
    public long? ExpenseCategoryId { get; init; }
    public IReadOnlyList<JournalLineDimensionDto> Dimensions { get; init; } = Array.Empty<JournalLineDimensionDto>();
}

public sealed class JournalEntryDto
{
    public long Id { get; init; }
    public string EntryNumber { get; init; } = string.Empty;
    public JournalType JournalType { get; init; }
    public string JournalTypeName { get; init; } = string.Empty;
    public JournalEntryStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTime EntryDate { get; init; }
    public long AccountingPeriodId { get; init; }
    public string PeriodName { get; init; } = string.Empty;
    public AccountingPeriodStatus PeriodStatus { get; init; }
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal ExchangeRate { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? Reference { get; init; }
    public decimal TotalDebit { get; init; }
    public decimal TotalCredit { get; init; }
    public decimal TotalDebitBase { get; init; }
    public decimal TotalCreditBase { get; init; }
    public bool IsBalanced { get; init; }
    public bool IsSystemGenerated { get; init; }
    public string? SourceType { get; init; }
    public long? SourceId { get; init; }
    public string? SourceNumber { get; init; }
    public string? SourceEventType { get; init; }
    public long? CreatedByUserId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? SubmittedAtUtc { get; init; }
    public long? SubmittedByUserId { get; init; }
    public DateTime? ApprovedAtUtc { get; init; }
    public long? ApprovedByUserId { get; init; }
    public DateTime? RejectedAtUtc { get; init; }
    public long? RejectedByUserId { get; init; }
    public string? RejectionReason { get; init; }
    public DateTime? PostedAtUtc { get; init; }
    public long? PostedByUserId { get; init; }
    public DateTime? ReversedAtUtc { get; init; }
    public long? ReversedByUserId { get; init; }
    public long? ReversalOfEntryId { get; init; }
    public string? ReversalOfEntryNumber { get; init; }
    public long? ReversedByEntryId { get; init; }
    public string? ReversedByEntryNumber { get; init; }
    public IReadOnlyList<JournalEntryLineDto> Lines { get; init; } = Array.Empty<JournalEntryLineDto>();
}

/// <summary>Result of a create with an Idempotency-Key: Replayed = the same request was already saved.</summary>
public sealed record JournalCreationResult(JournalEntryDto Entry, bool Replayed);
