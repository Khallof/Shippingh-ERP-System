using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Cash;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Accounting;

/// <summary>
/// Bank reconciliation (التسوية البنكية) of one bank account at a statement date:
///
///   Adjusted bank balance = statement ending balance + deposits in transit − outstanding withdrawals
///   Adjusted book balance = ledger balance + bank-only deposits − bank-only withdrawals
///   Difference            = adjusted bank − adjusted book (must be 0 to complete)
///
/// Completing it clears the matched ledger lines and statement lines for good.
/// </summary>
public class BankReconciliation : BaseEntity
{
    /// <summary>"BRC-2026-000001".</summary>
    public string ReconciliationNumber { get; set; } = string.Empty;

    public long BankAccountId { get; set; }

    public BankAccount BankAccount { get; set; } = null!;

    /// <summary>The general ledger account of the bank account.</summary>
    public long AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public DateTime StatementDate { get; set; }

    public decimal StatementEndingBalance { get; set; }

    /// <summary>Ledger balance of the bank account at the statement date (bank currency).</summary>
    public decimal BookBalance { get; set; }

    public decimal OutstandingDeposits { get; set; }

    public decimal OutstandingWithdrawals { get; set; }

    public decimal BankOnlyDeposits { get; set; }

    public decimal BankOnlyWithdrawals { get; set; }

    public decimal AdjustedBankBalance { get; set; }

    public decimal AdjustedBookBalance { get; set; }

    public decimal Difference { get; set; }

    public BankReconciliationStatus Status { get; set; } = BankReconciliationStatus.Draft;

    public DateTime? CompletedAtUtc { get; set; }

    public long? CompletedByUserId { get; set; }

    public string? Notes { get; set; }

    public ICollection<BankReconciliationLine> Lines { get; set; } = new List<BankReconciliationLine>();
}
