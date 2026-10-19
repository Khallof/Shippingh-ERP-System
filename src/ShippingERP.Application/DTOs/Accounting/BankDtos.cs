using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Accounting;

/// <summary>GET /api/v1/accounting/bank-transactions. Search: description, reference. SortBy: transactionDate (default).</summary>
public sealed class BankTransactionListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "transactionDate" };

    public long? BankAccountId { get; set; }

    public bool? IsReconciled { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }
}

public sealed class BankTransactionDto
{
    public long Id { get; init; }
    public long BankAccountId { get; init; }
    public string BankAccountName { get; init; } = string.Empty;
    public DateTime TransactionDate { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? Reference { get; init; }
    public decimal Deposit { get; init; }
    public decimal Withdrawal { get; init; }
    public bool IsReconciled { get; init; }
    public long? BankReconciliationId { get; init; }
    public long? MatchedJournalEntryLineId { get; init; }
}

/// <summary>A bank statement line: Deposit (money in) or Withdrawal (money out).</summary>
public sealed class BankStatementLineRequest
{
    public DateTime TransactionDate { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? Reference { get; set; }

    public decimal Deposit { get; set; }

    public decimal Withdrawal { get; set; }
}

/// <summary>A manual match: this statement line is this ledger line.</summary>
public sealed class BankMatchRequest
{
    public long BankTransactionId { get; set; }

    public long JournalEntryLineId { get; set; }
}

/// <summary>
/// Body of POST /api/v1/accounting/bank-reconciliation.
/// 1. StatementLines are saved as bank transactions (an identical unreconciled line is not imported twice).
/// 2. Matches are applied, then (AutoMatch) the remaining items are matched by amount, reference and date (±7 days).
/// 3. The reconciliation is calculated and saved as Draft; Complete = true completes it when the difference is 0
///    and no statement-only item remains (book those first with a journal entry, e.g. bank fees).
/// </summary>
public sealed class CreateBankReconciliationRequest
{
    public long BankAccountId { get; set; }

    public DateTime StatementDate { get; set; }

    public decimal StatementEndingBalance { get; set; }

    public List<BankStatementLineRequest> StatementLines { get; set; } = new();

    public List<BankMatchRequest> Matches { get; set; } = new();

    public bool AutoMatch { get; set; } = true;

    public bool Complete { get; set; }

    public string? Notes { get; set; }
}

public sealed class CompleteBankReconciliationRequest
{
    public string? Notes { get; set; }
}

/// <summary>GET /api/v1/accounting/bank-reconciliation. SortBy: statementDate (default, newest first).</summary>
public sealed class BankReconciliationListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "statementDate" };

    public long? BankAccountId { get; set; }

    public BankReconciliationStatus? Status { get; set; }
}

public sealed class BankReconciliationLineDto
{
    public long Id { get; init; }
    public BankReconciliationLineType LineType { get; init; }
    public string LineTypeName { get; init; } = string.Empty;
    public long? BankTransactionId { get; init; }
    public long? JournalEntryLineId { get; init; }
    public long? JournalEntryId { get; init; }
    public string? JournalEntryNumber { get; init; }
    public DateTime TransactionDate { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public bool IsCleared { get; init; }
}

public sealed class BankReconciliationDto
{
    public long Id { get; init; }
    public string ReconciliationNumber { get; init; } = string.Empty;
    public long BankAccountId { get; init; }
    public string BankAccountName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public long AccountId { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public DateTime StatementDate { get; init; }
    public decimal StatementEndingBalance { get; init; }
    public decimal BookBalance { get; init; }
    public decimal OutstandingDeposits { get; init; }
    public decimal OutstandingWithdrawals { get; init; }
    public decimal BankOnlyDeposits { get; init; }
    public decimal BankOnlyWithdrawals { get; init; }
    public decimal AdjustedBankBalance { get; init; }
    public decimal AdjustedBookBalance { get; init; }
    public decimal Difference { get; init; }
    public BankReconciliationStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTime? CompletedAtUtc { get; init; }
    public long? CompletedByUserId { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public int MatchedCount { get; init; }
    public int BankOnlyCount { get; init; }
    public int BookOnlyCount { get; init; }
    public IReadOnlyList<BankReconciliationLineDto> Lines { get; init; } = Array.Empty<BankReconciliationLineDto>();
}
