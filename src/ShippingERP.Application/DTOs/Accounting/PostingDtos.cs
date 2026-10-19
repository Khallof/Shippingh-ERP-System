using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Accounting;

/// <summary>GET /api/v1/accounting/posting-rules. Search: role, description. SortBy: accountRole (default), priority.</summary>
public sealed class PostingRuleListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "accountRole", "priority" };

    public string? AccountRole { get; set; }

    public long? AccountId { get; set; }

    public long? BranchId { get; set; }

    public bool? IsActive { get; set; }
}

public sealed class PostingRuleDto
{
    public long Id { get; init; }
    public string AccountRole { get; init; } = string.Empty;
    public long AccountId { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string AccountName { get; init; } = string.Empty;
    public long? BranchId { get; init; }
    public string? BranchName { get; init; }
    public long? CurrencyId { get; init; }
    public string? CurrencyCode { get; init; }
    public long? CashboxId { get; init; }
    public string? CashboxName { get; init; }
    public long? BankAccountId { get; init; }
    public string? BankAccountName { get; init; }
    public long? ExpenseCategoryId { get; init; }
    public string? ExpenseCategoryName { get; init; }
    public int Priority { get; init; }
    public int Specificity { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>Body of POST / PUT /api/v1/accounting/posting-rules.</summary>
public sealed class SavePostingRuleRequest
{
    /// <summary>One of the account roles (GET /api/v1/accounting/posting-rules/roles).</summary>
    public string AccountRole { get; set; } = string.Empty;

    /// <summary>A postable, active account.</summary>
    public long AccountId { get; set; }

    public long? BranchId { get; set; }

    public long? CurrencyId { get; set; }

    public long? CashboxId { get; set; }

    public long? BankAccountId { get; set; }

    public long? ExpenseCategoryId { get; set; }

    public int Priority { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>GET /api/v1/accounting/posting-rules/resolve: which account a posting line would use.</summary>
public sealed class ResolvePostingAccountQuery
{
    public string AccountRole { get; set; } = string.Empty;

    public long? BranchId { get; set; }

    public long? CurrencyId { get; set; }

    public long? CashboxId { get; set; }

    public long? BankAccountId { get; set; }

    public long? ExpenseCategoryId { get; set; }
}

public sealed class ResolvedPostingAccountDto
{
    public string AccountRole { get; init; } = string.Empty;
    public long AccountId { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string AccountName { get; init; } = string.Empty;

    /// <summary>The rule used (null when the bank account's own GL account was used).</summary>
    public long? RuleId { get; init; }
}

public sealed class AccountRoleDto
{
    public string Role { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
}

/// <summary>
/// GET /api/v1/accounting/posting-events. Search: source number, event type.
/// SortBy: occurredAtUtc (default, newest first).
/// </summary>
public sealed class PostingEventListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "occurredAtUtc" };

    public PostingEventStatus? Status { get; set; }

    public string? EventType { get; set; }

    public string? SourceType { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }
}

public sealed class PostingEventDto
{
    public long Id { get; init; }
    public Guid EventId { get; init; }
    public long? OutboxMessageId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string? SourceType { get; init; }
    public long? SourceId { get; init; }
    public string? SourceNumber { get; init; }
    public string? SourceKey { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public DateTime? PostingDate { get; init; }
    public PostingEventStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public long? JournalEntryId { get; init; }
    public string? JournalEntryNumber { get; init; }
    public int Attempts { get; init; }
    public string? LastError { get; init; }
    public DateTime? ProcessedAtUtc { get; init; }
}

/// <summary>GET /api/v1/accounting/posting-events/summary.</summary>
public sealed class PostingSummaryDto
{
    /// <summary>Outbox messages not processed yet (waiting for the dispatcher, or failing).</summary>
    public int PendingOutboxMessages { get; init; }
    public DateTime? OldestPendingAtUtc { get; init; }
    public int PostedEvents { get; init; }
    public int SkippedEvents { get; init; }
    public int DuplicateEvents { get; init; }
    public int FailedEvents { get; init; }
    public DateTime? LastProcessedAtUtc { get; init; }
}

public sealed class ProcessPostingsRequest
{
    /// <summary>How many outbox messages to process now (1..1000, default 200).</summary>
    public int MaxCount { get; set; } = 200;
}

public sealed class ProcessPostingsResultDto
{
    public int Processed { get; init; }
    public int Posted { get; init; }
    public int Skipped { get; init; }
    public int Duplicates { get; init; }
    public int Failed { get; init; }

    /// <summary>Messages still waiting after this run.</summary>
    public int Remaining { get; init; }
}

/// <summary>Body of POST /api/v1/accounting/posting-events/{id}/retry.</summary>
public sealed class RetryPostingEventRequest
{
    /// <summary>
    /// Optional accounting date to use instead of the event date (e.g. when the event's period is closed). It must
    /// be in an open period. The original date stays in the entry description.
    /// </summary>
    public DateTime? PostingDate { get; set; }
}
