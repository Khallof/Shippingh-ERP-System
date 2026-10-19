using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Accounting;

/// <summary>
/// The accounting side of one operational event of the outbox (حدث ترحيل): what was posted for it, or why not.
/// EventId is UNIQUE: an event can be processed only once, however many times the dispatcher delivers it.
/// </summary>
public class AccountingPostingEvent : BaseEntity
{
    public Guid EventId { get; set; }

    public long? OutboxMessageId { get; set; }

    /// <summary>"InvoiceIssued", "CodCollected"...</summary>
    public string EventType { get; set; } = string.Empty;

    public string? SourceType { get; set; }

    public long? SourceId { get; set; }

    public string? SourceNumber { get; set; }

    /// <summary>Business idempotency key (also stored on the journal entry).</summary>
    public string? SourceKey { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    /// <summary>The accounting date used (or tried).</summary>
    public DateTime? PostingDate { get; set; }

    public PostingEventStatus Status { get; set; }

    public long? JournalEntryId { get; set; }

    public JournalEntry? JournalEntry { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }
}
