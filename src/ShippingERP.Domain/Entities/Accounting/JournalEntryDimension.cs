using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Accounting;

/// <summary>The value of one analytical dimension on a journal line (at most one per dimension type).</summary>
public class JournalEntryDimension : BaseEntity
{
    public long JournalEntryLineId { get; set; }

    public JournalEntryLine JournalEntryLine { get; set; } = null!;

    public DimensionType DimensionType { get; set; }

    public long AccountingDimensionId { get; set; }

    public AccountingDimension AccountingDimension { get; set; } = null!;
}
