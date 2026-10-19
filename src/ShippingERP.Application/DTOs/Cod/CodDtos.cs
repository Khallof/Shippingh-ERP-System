using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Cod;

// ============================================================ COD transactions

/// <summary>
/// GET /api/v1/cod. Search: shipment number, tracking number, sender code / name.
/// SortBy: createdAtUtc (default, newest first), collectedAtUtc, expectedAmount, collectedAmount, netPayableAmount, status.
/// </summary>
public sealed class CodTransactionListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields =
        { "createdAtUtc", "collectedAtUtc", "expectedAmount", "collectedAmount", "netPayableAmount", "status" };

    public CODStatus? Status { get; set; }

    public long? SenderCustomerId { get; set; }

    public long? DriverId { get; set; }

    /// <summary>Origin branch of the shipment.</summary>
    public long? BranchId { get; set; }

    public long? CurrencyId { get; set; }

    public long? CashboxId { get; set; }

    public DateTime? CollectedFromUtc { get; set; }

    public DateTime? CollectedToUtc { get; set; }

    /// <summary>true = only CODs with a net amount still to pay to the sender.</summary>
    public bool? HasRemainingPayable { get; set; }
}

public sealed class CodTransactionListItemDto
{
    public long Id { get; init; }
    public long ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public ShipmentStatus ShipmentStatus { get; init; }
    public long SenderCustomerId { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public long? DriverId { get; init; }
    public string? DriverName { get; init; }
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public CODStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public decimal ExpectedAmount { get; init; }
    public decimal CollectedAmount { get; init; }
    public decimal NetPayableAmount { get; init; }
    public decimal SettledAmount { get; init; }
    public decimal RemainingPayable { get; init; }
    public DateTime? CollectedAtUtc { get; init; }
    public DateTime? ReceivedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class CodHistoryDto
{
    public long Id { get; init; }
    public CODTransactionType TransactionType { get; init; }
    public string TransactionTypeName { get; init; } = string.Empty;
    public CODStatus? FromStatus { get; init; }
    public CODStatus ToStatus { get; init; }
    public decimal Amount { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public long? DriverId { get; init; }
    public long? CashboxId { get; init; }
    public string? ReferenceType { get; init; }
    public long? ReferenceId { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? Notes { get; init; }
    public long? CreatedByUserId { get; init; }
}

public sealed class CodTransactionDto
{
    public long Id { get; init; }
    public long ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public ShipmentStatus ShipmentStatus { get; init; }
    public long SenderCustomerId { get; init; }
    public string SenderCode { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public long OriginBranchId { get; init; }
    public string OriginBranchName { get; init; } = string.Empty;
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public CODStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public CODStatus? StatusBeforeDispute { get; init; }

    public decimal ExpectedAmount { get; init; }
    public decimal CollectedAmount { get; init; }
    public DateTime? CollectedAtUtc { get; init; }
    public long? DriverId { get; init; }
    public string? DriverName { get; init; }
    public long? CollectedByUserId { get; init; }
    public string? CollectionDifferenceReason { get; init; }

    public long? ReceivedCashboxId { get; init; }
    public string? ReceivedCashboxName { get; init; }
    public long? ReceiptVoucherId { get; init; }
    public string? ReceiptVoucherNumber { get; init; }
    public DateTime? ReceivedAtUtc { get; init; }

    public decimal ShippingChargeDeduction { get; init; }
    public decimal CodCommissionDeduction { get; init; }
    public decimal TaxDeduction { get; init; }
    public decimal OtherFeesAmount { get; init; }
    public decimal CompensationAmount { get; init; }
    public bool ShippingChargeDeducted { get; init; }
    public decimal NetPayableAmount { get; init; }
    public DateTime? ReadyAtUtc { get; init; }

    public decimal SettledAmount { get; init; }
    public decimal RemainingPayable { get; init; }
    public bool DeductionsRecognized { get; init; }
    public DateTime? SettledAtUtc { get; init; }

    /// <summary>The Draft / Approved settlement that currently reserves this COD.</summary>
    public long? OpenSettlementId { get; init; }
    public string? OpenSettlementNumber { get; init; }

    public string? DisputeReason { get; init; }
    public DateTime? DisputedAtUtc { get; init; }
    public DateTime? CancelledAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }

    public IReadOnlyList<CodHistoryDto> History { get; init; } = Array.Empty<CodHistoryDto>();
}

/// <summary>
/// Body of POST /api/v1/cod/collect: the money collected from the recipient of a DELIVERED COD shipment.
///
/// Give the shipment by ShipmentId or TrackingNumber (scanned label).
/// - Driver collection (no CashboxId): the money goes to the driver's cash balance. DriverId is optional (default:
///   the driver of the proof of delivery / the shipment); a driver user always collects for himself.
/// - Counter collection (CashboxId): the recipient paid at the branch, the money goes straight into the cashbox
///   (needs COD.Handover).
/// An amount different from the shipment's COD amount needs DifferenceReason, and the COD becomes Disputed until
/// a supervisor resolves it.
/// </summary>
public sealed class CollectCodRequest
{
    public long? ShipmentId { get; set; }

    public string? TrackingNumber { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Default: now. Cannot be in the future or before the delivery.</summary>
    public DateTime? CollectedAtUtc { get; set; }

    public long? DriverId { get; set; }

    public long? CashboxId { get; set; }

    public string? DifferenceReason { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Body of POST /api/v1/cod/driver-handover (send an "Idempotency-Key" header): the cash a driver hands over.
///
/// CodTransactionIds = the COD shipments handed over (default: all COD shipments the driver collected in the
/// cashbox currency). Amount = the cash really counted. It cannot exceed the driver's balance (record an approved
/// DriverExcessCash adjustment first). Less than the COD total = shortage (stays on the driver's balance), more =
/// covers the driver's older balance.
/// </summary>
public sealed class DriverHandoverRequest
{
    public long DriverId { get; set; }

    public long CashboxId { get; set; }

    public decimal Amount { get; set; }

    public List<long> CodTransactionIds { get; set; } = new();

    public string? Notes { get; set; }
}

/// <summary>
/// Body of POST /api/v1/cod/mark-ready: reviewed COD shipments received in a cashbox become ready for the sender
/// settlement (the deductions and net amount are calculated now). Give CodTransactionIds, or SenderCustomerId to
/// take every received COD of that sender.
/// </summary>
public sealed class MarkCodReadyRequest
{
    public List<long> CodTransactionIds { get; set; } = new();

    public long? SenderCustomerId { get; set; }
}

public sealed class OpenCodDisputeRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class ResolveCodDisputeRequest
{
    public string Notes { get; set; } = string.Empty;
}

// ============================================================ adjustments

/// <summary>
/// Body of POST /api/v1/cod/adjustments. Fields by type:
///   DriverShortageWriteOff / DriverExcessCash : DriverId, Amount, CurrencyId (default: base currency)
///   CollectionCorrection                      : CodTransactionId, NewCollectedAmount
///   CollectionReversal                        : CodTransactionId
///   SenderFee / SenderCompensation            : CodTransactionId, Amount
/// Reason is always required. Nothing changes until another user approves it.
/// </summary>
public sealed class CreateCodAdjustmentRequest
{
    public CODAdjustmentType AdjustmentType { get; set; }

    public long? DriverId { get; set; }

    public long? CodTransactionId { get; set; }

    public long? CurrencyId { get; set; }

    public decimal? Amount { get; set; }

    public decimal? NewCollectedAmount { get; set; }

    public string Reason { get; set; } = string.Empty;
}

/// <summary>Approve (notes optional) or reject (notes required) an adjustment.</summary>
public sealed class DecideCodAdjustmentRequest
{
    public string? Notes { get; set; }
}

/// <summary>GET /api/v1/cod/adjustments. SortBy: createdAtUtc (default), amount.</summary>
public sealed class CodAdjustmentListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "createdAtUtc", "amount" };

    public CODAdjustmentStatus? Status { get; set; }

    public CODAdjustmentType? AdjustmentType { get; set; }

    public long? DriverId { get; set; }

    public long? CodTransactionId { get; set; }

    public long? BranchId { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }
}

public sealed class CodAdjustmentDto
{
    public long Id { get; init; }
    public string AdjustmentNumber { get; init; } = string.Empty;
    public CODAdjustmentType AdjustmentType { get; init; }
    public string AdjustmentTypeName { get; init; } = string.Empty;
    public CODAdjustmentStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public long? DriverId { get; init; }
    public string? DriverName { get; init; }
    public long? CodTransactionId { get; init; }
    public string? ShipmentNumber { get; init; }
    public decimal Amount { get; init; }
    public decimal? PreviousCollectedAmount { get; init; }
    public decimal? NewCollectedAmount { get; init; }
    public string Reason { get; init; } = string.Empty;
    public long? RequestedByUserId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? DecidedByUserId { get; init; }
    public DateTime? DecidedAtUtc { get; init; }
    public string? DecisionNotes { get; init; }
}

// ============================================================ driver balances

/// <summary>
/// GET /api/v1/cod/driver-balances. A driver user sees only his own accounts.
/// SortBy: balance (default, highest first), driverName, lastMovementAtUtc.
/// </summary>
public sealed class DriverBalanceQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "balance", "driverName", "lastMovementAtUtc" };

    public long? BranchId { get; set; }

    public long? CurrencyId { get; set; }

    public long? DriverId { get; set; }

    /// <summary>Default true: only accounts whose balance is not zero.</summary>
    public bool OnlyOutstanding { get; set; } = true;
}

public sealed class DriverBalanceDto
{
    public long DriverId { get; init; }
    public string DriverName { get; init; } = string.Empty;
    public string EmployeeCode { get; init; } = string.Empty;
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Balance { get; init; }

    /// <summary>COD shipments collected by the driver and not handed over yet.</summary>
    public int UnhandedCodCount { get; init; }
    public decimal UnhandedCodAmount { get; init; }
    public DateTime? OldestUnhandedCollectionAtUtc { get; init; }

    public DateTime? LastHandoverAtUtc { get; init; }
    public DateTime? LastMovementAtUtc { get; init; }
}

/// <summary>GET /api/v1/cod/driver-balances/{driverId}/movements (newest first).</summary>
public sealed class DriverMovementQuery : PagedQuery
{
    public long? CurrencyId { get; set; }

    public DriverCashMovementType? MovementType { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }
}

public sealed class DriverCashMovementDto
{
    public long Id { get; init; }
    public DriverCashMovementType MovementType { get; init; }
    public string MovementTypeName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
    public decimal BalanceAfter { get; init; }
    public DateTime MovementDateUtc { get; init; }
    public long? CodTransactionId { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public long DocumentId { get; init; }
    public string DocumentNumber { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public long? CreatedByUserId { get; init; }
}

// ============================================================ sender balances, aging, dashboard

/// <summary>
/// GET /api/v1/cod/sender-balances: what the company holds for each sender, by stage.
/// Search: sender code / name. SortBy: totalHeldAmount (default, highest first), senderName, readyNetAmount.
/// </summary>
public sealed class SenderBalanceQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "totalHeldAmount", "senderName", "readyNetAmount" };

    public long? SenderCustomerId { get; set; }

    public long? CurrencyId { get; set; }

    /// <summary>Origin branch of the shipments.</summary>
    public long? BranchId { get; set; }

    /// <summary>Default true: only senders with something pending or held.</summary>
    public bool OnlyWithBalance { get; set; } = true;
}

public sealed class SenderBalanceDto
{
    public long SenderCustomerId { get; init; }
    public string SenderCode { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;

    /// <summary>Not collected yet (Pending / AssignedToDriver): expected amounts.</summary>
    public int PendingCount { get; init; }
    public decimal PendingAmount { get; init; }

    /// <summary>Collected, still with the drivers.</summary>
    public decimal WithDriversAmount { get; init; }

    /// <summary>In branch cashboxes, not reviewed yet.</summary>
    public decimal InCashboxAmount { get; init; }

    /// <summary>Remaining NET amount ready to pay (ready + partially settled).</summary>
    public decimal ReadyNetAmount { get; init; }

    public decimal DisputedAmount { get; init; }

    /// <summary>Sender money held by the company (drivers + cashboxes + ready + disputed).</summary>
    public decimal TotalHeldAmount { get; init; }

    /// <summary>Amount reserved in Draft / Approved settlements.</summary>
    public decimal OpenSettlementAmount { get; init; }
}

/// <summary>GET /api/v1/cod/aging: outstanding COD (delivered, not paid to the sender) by age, as of a date.</summary>
public sealed class CodAgingQuery
{
    /// <summary>Default: now.</summary>
    public DateTime? AsOfUtc { get; set; }

    /// <summary>Default: the base currency.</summary>
    public long? CurrencyId { get; set; }

    public long? BranchId { get; set; }

    public long? SenderCustomerId { get; set; }
}

public sealed class CodAgingRowDto
{
    public long SenderCustomerId { get; init; }
    public string SenderCode { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public int Count { get; init; }

    // Amounts by age (days since collection; since delivery for delivered COD not collected yet).
    public decimal Days0To7 { get; init; }
    public decimal Days8To15 { get; init; }
    public decimal Days16To30 { get; init; }
    public decimal Days31To60 { get; init; }
    public decimal Over60Days { get; init; }
    public decimal Total { get; init; }

    // The same total by stage.
    public decimal DeliveredNotCollected { get; init; }
    public decimal WithDrivers { get; init; }
    public decimal InCashboxes { get; init; }
    public decimal ReadyForSettlement { get; init; }
    public decimal Disputed { get; init; }

    public DateTime? OldestDateUtc { get; init; }
}

public sealed class CodAgingReportDto
{
    public DateTime AsOfUtc { get; init; }
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public IReadOnlyList<CodAgingRowDto> Rows { get; init; } = Array.Empty<CodAgingRowDto>();
    public CodAgingRowDto Totals { get; init; } = new();
}

/// <summary>GET /api/v1/cod/dashboard.</summary>
public sealed class CodDashboardQuery
{
    public long? BranchId { get; set; }

    /// <summary>Default: the base currency.</summary>
    public long? CurrencyId { get; set; }
}

public sealed class CodStageSummaryDto
{
    public CODStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public int Count { get; init; }

    /// <summary>Expected amount (not collected), collected amount, or remaining net amount (ready / partial).</summary>
    public decimal Amount { get; init; }
}

public sealed class CodDashboardDto
{
    public DateTime AsOfUtc { get; init; }
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public IReadOnlyList<CodStageSummaryDto> Stages { get; init; } = Array.Empty<CodStageSummaryDto>();

    /// <summary>Delivered shipments whose COD was not recorded yet (follow up with the drivers).</summary>
    public int DeliveredNotCollectedCount { get; init; }
    public decimal DeliveredNotCollectedAmount { get; init; }

    public int DriversWithBalance { get; init; }
    public decimal DriversOutstandingTotal { get; init; }

    public int CashboxCount { get; init; }
    public decimal CashboxBalanceTotal { get; init; }

    public decimal UnsettledSenderNet { get; init; }

    public int OpenSettlementsCount { get; init; }
    public decimal OpenSettlementsAmount { get; init; }

    public int PendingAdjustments { get; init; }
    public int PendingTransfers { get; init; }
    public decimal InTransitTransfersAmount { get; init; }
    public int PendingClosingApprovals { get; init; }
}

/// <summary>GET /api/v1/reports/driver-collections: collections and handovers per driver in a period.</summary>
public sealed class DriverCollectionReportQuery
{
    /// <summary>Default: the start of the local business day 29 days before the end (30 days in all).</summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>Exclusive. Default: the end of today (local business day).</summary>
    public DateTime? ToUtc { get; set; }

    /// <summary>Default: the base currency.</summary>
    public long? CurrencyId { get; set; }

    public long? BranchId { get; set; }

    public long? DriverId { get; set; }
}

public sealed class DriverCollectionRowDto
{
    public long DriverId { get; init; }
    public string DriverName { get; init; } = string.Empty;
    public string BranchName { get; init; } = string.Empty;
    public decimal OpeningBalance { get; init; }
    public int CollectedCount { get; init; }
    public decimal CollectedAmount { get; init; }
    public int HandoverCount { get; init; }
    public decimal HandedOverAmount { get; init; }
    public decimal ShortageWrittenOff { get; init; }
    public decimal ExcessRecorded { get; init; }
    /// <summary>Corrections, reversals and cancelled handovers (signed: + the driver holds more).</summary>
    public decimal OtherMovements { get; init; }
    public decimal ClosingBalance { get; init; }
}

public sealed class DriverCollectionReportDto
{
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc { get; init; }
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public IReadOnlyList<DriverCollectionRowDto> Rows { get; init; } = Array.Empty<DriverCollectionRowDto>();
    public DriverCollectionRowDto Totals { get; init; } = new();
}
