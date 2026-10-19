using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.Accounting;

/// <summary>
/// Links a posted entry with the entry that reverses it (قيد عكسي). Each entry can be reversed once only
/// (unique OriginalEntryId) and a reversal entry reverses exactly one entry (unique ReversalEntryId).
/// Append-only: the history of corrections is never rewritten.
/// </summary>
public class ReversalJournalLink : BaseEntity, IAppendOnlyEntity
{
    public long OriginalEntryId { get; set; }

    public JournalEntry OriginalEntry { get; set; } = null!;

    public long ReversalEntryId { get; set; }

    public JournalEntry ReversalEntry { get; set; } = null!;

    public string Reason { get; set; } = string.Empty;

    /// <summary>True when the source document was cancelled (automatic), false for a manual reversal.</summary>
    public bool IsAutomatic { get; set; }
}
