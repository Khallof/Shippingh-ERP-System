using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Accounting;

/// <summary>
/// Journal entry header (قيد اليومية). The lines carry the amounts; the header keeps the workflow, the source
/// document and the totals.
///
/// Rules (JournalRules, JournalIntegrityInterceptor and SQL Server triggers):
/// - Posted entries are balanced: TotalDebit = TotalCredit and TotalDebitBase = TotalCreditBase.
/// - Posted / Reversed entries and their lines are never edited or deleted. A correction is a reversal entry
///   (ReversalJournalLink) or a new adjustment entry.
/// - Automatic entries carry a unique SourceKey ("InvoiceIssued:Invoice:15"): the same document can never be
///   posted twice.
/// </summary>
public class JournalEntry : BaseEntity
{
    /// <summary>
    /// "JV-2026-000001" once in the ledger (posted entries are numbered without gaps in posting order);
    /// "DRF-2026-000001" while a manual entry is a draft / waiting for approval.
    /// </summary>
    public string EntryNumber { get; set; } = string.Empty;

    public JournalType JournalType { get; set; } = JournalType.General;

    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Draft;

    /// <summary>Accounting date (SQL "date"): decides the period.</summary>
    public DateTime EntryDate { get; set; }

    public long AccountingPeriodId { get; set; }

    public AccountingPeriod AccountingPeriod { get; set; } = null!;

    /// <summary>Main branch of the entry (each line also has its own branch).</summary>
    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    /// <summary>Currency of the line amounts (Debit / Credit).</summary>
    public long CurrencyId { get; set; }

    public Currency Currency { get; set; } = null!;

    /// <summary>1 base currency unit = amount × ExchangeRate. 1 for the base currency.</summary>
    public decimal ExchangeRate { get; set; } = 1m;

    public string Description { get; set; } = string.Empty;

    /// <summary>Free external reference (bank notice, supplier invoice number...).</summary>
    public string? Reference { get; set; }

    public decimal TotalDebit { get; set; }

    public decimal TotalCredit { get; set; }

    public decimal TotalDebitBase { get; set; }

    public decimal TotalCreditBase { get; set; }

    /// <summary>True for entries created by the automatic posting of operational events.</summary>
    public bool IsSystemGenerated { get; set; }

    /// <summary>"Invoice", "CashReceiptVoucher", "SenderSettlement"... (automatic entries).</summary>
    public string? SourceType { get; set; }

    public long? SourceId { get; set; }

    /// <summary>The source document number (INV-2026-000003, RCV-2026-000010...).</summary>
    public string? SourceNumber { get; set; }

    /// <summary>The operational event type ("InvoiceIssued", "CodCollected"...).</summary>
    public string? SourceEventType { get; set; }

    /// <summary>
    /// Idempotency key, unique when not null: automatic entries "EventType:SourceType:SourceId", manual entries
    /// created with an Idempotency-Key header "manual:{userId}:{key}", manual reversals "reversal:{entryId}".
    /// </summary>
    public string? SourceKey { get; set; }

    /// <summary>SHA-256 of the body of a manual entry created with an Idempotency-Key (same key + other body = 409).</summary>
    public string? RequestHash { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public long? SubmittedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public long? ApprovedByUserId { get; set; }

    public DateTime? RejectedAtUtc { get; set; }

    public long? RejectedByUserId { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime? PostedAtUtc { get; set; }

    public long? PostedByUserId { get; set; }

    public DateTime? ReversedAtUtc { get; set; }

    public long? ReversedByUserId { get; set; }

    /// <summary>On a reversal entry: the entry it cancels.</summary>
    public long? ReversalOfEntryId { get; set; }

    public JournalEntry? ReversalOfEntry { get; set; }

    /// <summary>On a reversed entry: the reversal entry that cancels it.</summary>
    public long? ReversedByEntryId { get; set; }

    public JournalEntry? ReversedByEntry { get; set; }

    public ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
}
