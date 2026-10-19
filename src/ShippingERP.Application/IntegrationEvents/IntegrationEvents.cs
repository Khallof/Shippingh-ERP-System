namespace ShippingERP.Application.IntegrationEvents;

/// <summary>
/// Base of the events other modules subscribe to. They are saved in the transactional outbox (OutboxMessages) in
/// the same database transaction as the business change, then delivered by the accounting dispatcher (step 7).
/// Events are immutable facts: never change the meaning of a property, add new ones instead.
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTime OccurredAtUtc { get; init; }

    /// <summary>The user who caused the event (null for background jobs).</summary>
    public long? UserId { get; init; }

    /// <summary>Stable event name stored in OutboxMessage.EventType.</summary>
    public abstract string EventType { get; }
}

/// <summary>Adds integration events to the current unit of work (does NOT save; the caller's SaveChanges does).</summary>
public interface IIntegrationEventPublisher
{
    void Publish(IntegrationEvent integrationEvent);
}

/// <summary>
/// Step 7: the accounting module reads the outbox (AccountingPostingProcessor in Infrastructure/Accounting, run by
/// the AccountingPostingBackgroundService) and turns the events into posted journal entries:
///   InvoiceIssued           -> Dr Accounts receivable  Cr Shipping / COD commission / additional service revenue  Cr Tax payable
///   InvoiceCancelled        -> the exact reversal of the InvoiceIssued entry
///   CreditNoteIssued        -> Dr Revenue (same split as the invoice)  Dr Tax payable  Cr Accounts receivable
///   CustomerPaymentReceived -> Dr Cash clearing (cash) / Bank (bank, card, wallet)  Cr Accounts receivable
///   CustomerPaymentAllocated-> no accounting effect (receivable sub-ledger only)
///   Step 6 events            -> their own AccountingPostingRequest (account roles mapped to accounts by the posting rules)
/// Processing is idempotent: one AccountingPostingEvent per EventId (unique) and one journal entry per business
/// document (unique JournalEntry.SourceKey), so an event delivered twice never posts twice.
/// </summary>
public interface IAccountingIntegrationHandler
{
    /// <summary>Posts the journal entry of one outbox message (idempotent). Returns false when the message is unknown.</summary>
    Task<bool> HandleOutboxMessageAsync(long outboxMessageId, CancellationToken cancellationToken);
}
