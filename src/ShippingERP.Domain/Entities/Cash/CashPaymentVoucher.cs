using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.HR;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Cash;

/// <summary>
/// Cash payment voucher (سند صرف نقدي), number PMV-2026-000001: money that left a cashbox.
/// Type SenderSettlement is created when a settlement is paid in cash (and is cancelled only by reversing that
/// settlement). The other types are entered with POST /cash-payments. The cashbox balance must cover the amount.
/// </summary>
public class CashPaymentVoucher : BaseEntity
{
    public string VoucherNumber { get; set; } = string.Empty;

    public long CashboxId { get; set; }

    public Cashbox Cashbox { get; set; } = null!;

    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public long CurrencyId { get; set; }

    public Currency Currency { get; set; } = null!;

    public CashPaymentType PaymentType { get; set; }

    public CashVoucherStatus Status { get; set; } = CashVoucherStatus.Posted;

    public DateTime VoucherDateUtc { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Name of the receiver as printed.</summary>
    public string PaidTo { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Invoice number of the supplier, receipt number...</summary>
    public string? ReferenceNumber { get; set; }

    // ---------------- Links (by type) ----------------

    public long? ExpenseCategoryId { get; set; }

    public ExpenseCategory? ExpenseCategory { get; set; }

    public long? SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    public long? EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public long? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public long? SenderSettlementId { get; set; }

    // ---------------- Cancellation ----------------

    public DateTime? CancelledAtUtc { get; set; }

    public long? CancelledByUserId { get; set; }

    public string? CancellationReason { get; set; }

    // ---------------- Idempotency ----------------

    public string? IdempotencyKey { get; set; }

    public string? RequestHash { get; set; }
}
