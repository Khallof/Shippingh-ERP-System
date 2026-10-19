using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Accounting;

/// <summary>
/// Accounting period (الفترة المحاسبية), usually a month. A journal entry belongs to the period that contains its
/// EntryDate. When a period is Closed nothing can be posted into it any more: corrections go into an open period.
/// </summary>
public class AccountingPeriod : BaseEntity
{
    public long FiscalYearId { get; set; }

    public FiscalYear FiscalYear { get; set; } = null!;

    /// <summary>1..n inside the fiscal year.</summary>
    public int PeriodNumber { get; set; }

    /// <summary>"كانون الثاني 2026".</summary>
    public string Name { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public AccountingPeriodStatus Status { get; set; } = AccountingPeriodStatus.Open;

    public DateTime? ClosedAtUtc { get; set; }

    public long? ClosedByUserId { get; set; }

    public string? CloseNotes { get; set; }

    public DateTime? ReopenedAtUtc { get; set; }

    public long? ReopenedByUserId { get; set; }

    public string? ReopenReason { get; set; }
}
