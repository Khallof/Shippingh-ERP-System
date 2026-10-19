using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.Integration;

/// <summary>
/// Transactional outbox (صندوق الأحداث الصادرة). Business events for other modules (first: the accounting module of
/// step 7, which posts the journal entries) are saved here IN THE SAME TRANSACTION as the business change, so an
/// event is never lost and never published for a change that was rolled back.
///
/// The accounting dispatcher (step 7) reads rows with ProcessedAtUtc = null in Id order, posts them and sets
/// ProcessedAtUtc in the same transaction as the journal entry. EventId (unique in AccountingPostingEvents) makes
/// a second delivery harmless. Attempts / LastError record failures (closed period, unmapped account...).
/// </summary>
public class OutboxMessage : BaseEntity
{
    public Guid EventId { get; set; }

    /// <summary>Event name, e.g. "InvoiceIssued".</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Event body (camelCase JSON).</summary>
    public string PayloadJson { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}
