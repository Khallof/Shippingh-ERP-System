using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Accounting;

/// <summary>
/// Fiscal year (السنة المالية), split into accounting periods (one per month). Dates are calendar dates
/// (stored as SQL "date"), inclusive on both ends.
/// </summary>
public class FiscalYear : BaseEntity
{
    /// <summary>"FY2026".</summary>
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public FiscalYearStatus Status { get; set; } = FiscalYearStatus.Open;

    public DateTime? ClosedAtUtc { get; set; }

    public long? ClosedByUserId { get; set; }

    public ICollection<AccountingPeriod> Periods { get; set; } = new List<AccountingPeriod>();
}
