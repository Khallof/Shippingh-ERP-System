using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Cash;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Cod;

/// <summary>
/// Sender settlement (تسوية مبالغ التحصيل مع المرسل), number SST-2026-000001: pays a sender the net amount of
/// one or more ready COD shipments, fully or partially.
///
/// Draft (created, CODs reserved) -> Approved (by another user) -> Paid (cash from a cashbox, or bank transfer).
/// Draft / Approved -> Cancelled. Paid -> Reversed (only the latest payment of its CODs, with a reason).
///
/// Totals are the sums of the lines. NetPayable = TotalGrossReleased - deductions + TotalCompensation, i.e. the
/// money that leaves the company. The deductions become company revenue when the settlement is paid.
/// </summary>
public class SenderSettlement : BaseEntity
{
    public string SettlementNumber { get; set; } = string.Empty;

    public long SenderCustomerId { get; set; }

    public Customer SenderCustomer { get; set; } = null!;

    /// <summary>The branch that pays the sender.</summary>
    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public long CurrencyId { get; set; }

    public Currency Currency { get; set; } = null!;

    public SettlementStatus Status { get; set; } = SettlementStatus.Draft;

    public DateTime SettlementDateUtc { get; set; }

    // ---------------- Totals ----------------

    /// <summary>Sender money (COD collected) released by this settlement.</summary>
    public decimal TotalGrossReleased { get; set; }

    public decimal TotalShippingCharges { get; set; }

    public decimal TotalCodCommission { get; set; }

    public decimal TotalTax { get; set; }

    public decimal TotalOtherFees { get; set; }

    public decimal TotalCompensation { get; set; }

    /// <summary>The amount paid to the sender (always &gt; 0).</summary>
    public decimal NetPayable { get; set; }

    public string? Notes { get; set; }

    // ---------------- Approval ----------------

    public long? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    // ---------------- Payment ----------------

    public long? PaymentMethodId { get; set; }

    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>Cash payments: the cashbox the money left.</summary>
    public long? CashboxId { get; set; }

    public Cashbox? Cashbox { get; set; }

    /// <summary>Bank payments: the company account the transfer was made from.</summary>
    public long? BankAccountId { get; set; }

    public BankAccount? BankAccount { get; set; }

    public string? PaymentReference { get; set; }

    /// <summary>Cash payments: the payment voucher printed for the sender.</summary>
    public long? CashPaymentVoucherId { get; set; }

    public CashPaymentVoucher? CashPaymentVoucher { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    public long? PaidByUserId { get; set; }

    // ---------------- Cancellation / reversal ----------------

    public DateTime? CancelledAtUtc { get; set; }

    public string? CancellationReason { get; set; }

    public DateTime? ReversedAtUtc { get; set; }

    public long? ReversedByUserId { get; set; }

    public string? ReversalReason { get; set; }

    public ICollection<SenderSettlementLine> Lines { get; set; } = new List<SenderSettlementLine>();
}
