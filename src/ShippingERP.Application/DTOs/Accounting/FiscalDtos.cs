using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Accounting;

/// <summary>
/// Body of POST /api/v1/accounting/fiscal-years. The monthly periods are created automatically.
/// Default EndDate: one year after StartDate minus one day. Default Code: "FY" + the start year.
/// </summary>
public sealed class CreateFiscalYearRequest
{
    public string? Code { get; set; }

    public string? ArabicName { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }
}

public sealed class UpdateFiscalYearRequest
{
    public string ArabicName { get; set; } = string.Empty;
}

public sealed class FiscalYearDto
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public FiscalYearStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTime? ClosedAtUtc { get; init; }
    public long? ClosedByUserId { get; init; }
    public int PeriodCount { get; init; }
    public int OpenPeriodCount { get; init; }
    public IReadOnlyList<AccountingPeriodDto> Periods { get; init; } = Array.Empty<AccountingPeriodDto>();
}

/// <summary>GET /api/v1/accounting/periods. SortBy: startDate (default).</summary>
public sealed class AccountingPeriodListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "startDate" };

    public long? FiscalYearId { get; set; }

    public AccountingPeriodStatus? Status { get; set; }

    /// <summary>Only the period containing this date.</summary>
    public DateTime? Date { get; set; }
}

public sealed class AccountingPeriodDto
{
    public long Id { get; init; }
    public long FiscalYearId { get; init; }
    public string FiscalYearCode { get; init; } = string.Empty;
    public int PeriodNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public AccountingPeriodStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTime? ClosedAtUtc { get; init; }
    public long? ClosedByUserId { get; init; }
    public string? CloseNotes { get; init; }
    public DateTime? ReopenedAtUtc { get; init; }
    public long? ReopenedByUserId { get; init; }
    public string? ReopenReason { get; init; }
}

public sealed class UpdateAccountingPeriodRequest
{
    public string Name { get; set; } = string.Empty;
}

public sealed class CloseAccountingPeriodRequest
{
    public string? Notes { get; set; }
}

public sealed class ReopenAccountingPeriodRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>GET /api/v1/accounting/periods/{id}/close-check: what still blocks the closing.</summary>
public sealed class PeriodCloseCheckDto
{
    public long PeriodId { get; init; }
    public string PeriodName { get; init; } = string.Empty;
    public AccountingPeriodStatus Status { get; init; }

    /// <summary>Draft / rejected manual entries dated in the period (post or delete them, or change their date).</summary>
    public int UnpostedEntries { get; init; }

    /// <summary>Submitted / approved entries waiting for a decision.</summary>
    public int EntriesAwaitingApproval { get; init; }

    /// <summary>Operational events that occurred up to the end of the period and are not posted yet.</summary>
    public int PendingPostingEvents { get; init; }

    /// <summary>Failed automatic postings dated in the period.</summary>
    public int FailedPostingEvents { get; init; }

    public bool CanClose { get; init; }

    public IReadOnlyList<string> Blockers { get; init; } = Array.Empty<string>();
}
