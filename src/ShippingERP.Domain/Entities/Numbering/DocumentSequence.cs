using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.Numbering;

/// <summary>
/// Last number used per document type and year (SHP 2026 -> 15 means SHP-2026-000015 was the last one).
/// Incremented atomically by the DocumentNumberGenerator inside the caller's transaction, so two users can
/// never get the same number. Numbers can have gaps (a rolled-back transaction), never duplicates.
/// </summary>
public class DocumentSequence : BaseEntity
{
    /// <summary>Document prefix, e.g. "SHP", "BTR".</summary>
    public string Name { get; set; } = string.Empty;

    public int Year { get; set; }

    public long LastNumber { get; set; }
}
