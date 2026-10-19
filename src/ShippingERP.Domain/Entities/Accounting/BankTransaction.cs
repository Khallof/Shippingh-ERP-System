using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Cash;

namespace ShippingERP.Domain.Entities.Accounting;

/// <summary>
/// A line of a bank statement (حركة كشف الحساب البنكي), entered or imported for the bank reconciliation.
/// Deposit = money in, Withdrawal = money out (exactly one is greater than zero).
/// </summary>
public class BankTransaction : BaseEntity
{
    public long BankAccountId { get; set; }

    public BankAccount BankAccount { get; set; } = null!;

    /// <summary>Statement date (SQL "date").</summary>
    public DateTime TransactionDate { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? Reference { get; set; }

    public decimal Deposit { get; set; }

    public decimal Withdrawal { get; set; }

    /// <summary>True once a completed reconciliation cleared it.</summary>
    public bool IsReconciled { get; set; }

    public long? BankReconciliationId { get; set; }

    public BankReconciliation? BankReconciliation { get; set; }

    /// <summary>The ledger line it was matched with (completed reconciliation).</summary>
    public long? MatchedJournalEntryLineId { get; set; }

    /// <summary>Signed amount: deposit positive, withdrawal negative.</summary>
    public decimal SignedAmount => Deposit - Withdrawal;
}
