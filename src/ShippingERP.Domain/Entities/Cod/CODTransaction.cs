using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Cash;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.HR;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Cod;

/// <summary>
/// The COD (الدفع عند الاستلام) amount of one shipment and where that money is now: one row per COD shipment.
///
/// It is created and kept in sync AUTOMATICALLY with the shipment (CodShipmentSyncInterceptor) while nothing is
/// collected: Pending / AssignedToDriver / Cancelled. From collection on, only the COD services change it, and
/// every change adds an immutable CODTransactionHistory row. Money is never stored here twice: the driver,
/// cashbox and sender amounts live in their own sub-ledgers (DriverCashMovement, CashboxTransaction,
/// SenderSettlementLine); this row only keeps the per-shipment summary used by the workflow and the reports.
///
/// The COD amount is NEVER company revenue: it is money held on behalf of the sender. Only the deductions
/// (shipping charge, COD commission, tax, approved fees) become revenue, when the settlement is paid.
///
/// Net payable to the sender = CollectedAmount - ShippingChargeDeduction - CodCommissionDeduction - TaxDeduction
///                             - OtherFeesAmount + CompensationAmount   (calculated when marked ready).
/// </summary>
public class CODTransaction : BaseEntity
{
    public long ShipmentId { get; set; }

    public Shipment Shipment { get; set; } = null!;

    public long SenderCustomerId { get; set; }

    public Customer SenderCustomer { get; set; } = null!;

    /// <summary>The shipment's origin branch: the branch that settles with the sender by default.</summary>
    public long OriginBranchId { get; set; }

    public Branch OriginBranch { get; set; } = null!;

    public long CurrencyId { get; set; }

    public Currency Currency { get; set; } = null!;

    public CODStatus Status { get; set; } = CODStatus.Pending;

    /// <summary>The status to go back to when a dispute is resolved (null when not disputed).</summary>
    public CODStatus? StatusBeforeDispute { get; set; }

    /// <summary>Shipment.CODAmount: what the recipient must pay.</summary>
    public decimal ExpectedAmount { get; set; }

    // ---------------- Collection ----------------

    /// <summary>What was really collected (0 before collection).</summary>
    public decimal CollectedAmount { get; set; }

    public DateTime? CollectedAtUtc { get; set; }

    /// <summary>The driver who has the shipment (AssignedToDriver) or collected the money. Null for counter collections.</summary>
    public long? DriverId { get; set; }

    public DriverProfile? Driver { get; set; }

    /// <summary>The user who recorded the collection (the driver himself or branch staff).</summary>
    public long? CollectedByUserId { get; set; }

    /// <summary>Required when the collected amount differs from the expected amount (the COD is then Disputed).</summary>
    public string? CollectionDifferenceReason { get; set; }

    // ---------------- Cashbox ----------------

    public long? ReceivedCashboxId { get; set; }

    public Cashbox? ReceivedCashbox { get; set; }

    /// <summary>The receipt voucher (driver handover or counter collection) that brought the money into the cashbox.</summary>
    public long? ReceiptVoucherId { get; set; }

    public CashReceiptVoucher? ReceiptVoucher { get; set; }

    public DateTime? ReceivedAtUtc { get; set; }

    // ---------------- Deductions and net (calculated when marked ready) ----------------

    /// <summary>Shipping charge after discount (0 when the shipment is billed on an invoice instead).</summary>
    public decimal ShippingChargeDeduction { get; set; }

    public decimal CodCommissionDeduction { get; set; }

    public decimal TaxDeduction { get; set; }

    /// <summary>Sum of the approved SenderFee adjustments.</summary>
    public decimal OtherFeesAmount { get; set; }

    /// <summary>Sum of the approved SenderCompensation adjustments.</summary>
    public decimal CompensationAmount { get; set; }

    /// <summary>
    /// True when the shipment charge is collected through this COD (deducted from the sender's money). Such a
    /// shipment can no longer be invoiced or repriced.
    /// </summary>
    public bool ShippingChargeDeducted { get; set; }

    public decimal NetPayableAmount { get; set; }

    public DateTime? ReadyAtUtc { get; set; }

    // ---------------- Settlement ----------------

    /// <summary>Sum of the paid settlement lines.</summary>
    public decimal SettledAmount { get; set; }

    /// <summary>True once a paid settlement line recognized the deductions (they are recognized only once).</summary>
    public bool DeductionsRecognized { get; set; }

    public DateTime? SettledAtUtc { get; set; }

    // ---------------- Dispute / cancellation ----------------

    public string? DisputeReason { get; set; }

    public DateTime? DisputedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public ICollection<CODTransactionHistory> History { get; set; } = new List<CODTransactionHistory>();

    public ICollection<SenderSettlementLine> SettlementLines { get; set; } = new List<SenderSettlementLine>();
}
