using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Cash;

/// <summary>
/// Cash transfer (تحويل نقدية), number CTR-2026-000001, between two cashboxes of the same currency (same branch or
/// another branch) or from a cashbox to a bank account.
///
/// PendingApproval: nothing moved yet. A SECOND user approves it: the money leaves the source cashbox.
///  - same branch: it enters the destination at once (Completed);
///  - another branch: it is "in transit" until the destination branch confirms receipt (Completed);
///  - bank: Completed, with the bank deposit reference.
/// Pending transfers can be rejected (approver) or cancelled (requester).
/// </summary>
public class CashTransfer : BaseEntity
{
    public string TransferNumber { get; set; } = string.Empty;

    public CashTransferType TransferType { get; set; }

    public CashTransferStatus Status { get; set; } = CashTransferStatus.PendingApproval;

    public long FromCashboxId { get; set; }

    public Cashbox FromCashbox { get; set; } = null!;

    public long FromBranchId { get; set; }

    public long? ToCashboxId { get; set; }

    public Cashbox? ToCashbox { get; set; }

    public long? ToBranchId { get; set; }

    public long? ToBankAccountId { get; set; }

    public BankAccount? ToBankAccount { get; set; }

    public long CurrencyId { get; set; }

    public Currency Currency { get; set; } = null!;

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>Bank deposit slip number (bank transfers).</summary>
    public string? BankReference { get; set; }

    public long? DecidedByUserId { get; set; }

    public DateTime? DecidedAtUtc { get; set; }

    /// <summary>Rejection or cancellation reason.</summary>
    public string? DecisionNotes { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public DateTime? ReceivedAtUtc { get; set; }

    public long? ReceivedByUserId { get; set; }
}
