using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Billing;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.HR;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Cash;

/// <summary>
/// Cash receipt voucher (سند قبض نقدي), number RCV-2026-000001: money that entered a cashbox.
///
/// Types DriverHandover and CounterCodCollection are created by the COD workflow (a driver handover receipt is a
/// receipt voucher with the driver, the expected amount and the shortage / excess). The other types are entered
/// with POST /cash-receipts. A posted voucher is never edited: it is cancelled with a reason, which adds a
/// reversing cashbox transaction (and, for COD vouchers, gives the money back to the driver / COD workflow).
/// </summary>
public class CashReceiptVoucher : BaseEntity
{
    public string VoucherNumber { get; set; } = string.Empty;

    public long CashboxId { get; set; }

    public Cashbox Cashbox { get; set; } = null!;

    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public long CurrencyId { get; set; }

    public Currency Currency { get; set; } = null!;

    public CashReceiptType ReceiptType { get; set; }

    public CashVoucherStatus Status { get; set; } = CashVoucherStatus.Posted;

    public DateTime VoucherDateUtc { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Name of the payer as printed (driver, customer, other).</summary>
    public string ReceivedFrom { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? ReferenceNumber { get; set; }

    // ---------------- Links ----------------

    public long? DriverId { get; set; }

    public DriverProfile? Driver { get; set; }

    public long? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    /// <summary>CustomerPayment type: the step-5 cash payment whose money is deposited here (once).</summary>
    public long? CustomerPaymentId { get; set; }

    public CustomerPayment? CustomerPayment { get; set; }

    // ---------------- Driver handover ----------------

    /// <summary>Handover: the total collected amount of the COD shipments handed over.</summary>
    public decimal? ExpectedAmount { get; set; }

    /// <summary>Handover: expected - counted when the driver brought less (stays on his balance).</summary>
    public decimal ShortageAmount { get; set; }

    /// <summary>Handover: counted - expected when the driver brought more (covers his older balance).</summary>
    public decimal ExcessAmount { get; set; }

    // ---------------- Cancellation ----------------

    public DateTime? CancelledAtUtc { get; set; }

    public long? CancelledByUserId { get; set; }

    public string? CancellationReason { get; set; }

    // ---------------- Idempotency ----------------

    public string? IdempotencyKey { get; set; }

    public string? RequestHash { get; set; }
}
