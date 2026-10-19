using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Accounting;

/// <summary>One item of a bank reconciliation: a matched pair, a statement-only line or a ledger-only line.</summary>
public class BankReconciliationLine : BaseEntity
{
    public long BankReconciliationId { get; set; }

    public BankReconciliation BankReconciliation { get; set; } = null!;

    public BankReconciliationLineType LineType { get; set; }

    public long? BankTransactionId { get; set; }

    public BankTransaction? BankTransaction { get; set; }

    public long? JournalEntryLineId { get; set; }

    public JournalEntryLine? JournalEntryLine { get; set; }

    public DateTime TransactionDate { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>Signed: + money in the bank, − money out.</summary>
    public decimal Amount { get; set; }

    /// <summary>True when the reconciliation is completed and the item is matched (cleared once only).</summary>
    public bool IsCleared { get; set; }
}
