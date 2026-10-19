using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Cod;

/// <summary>
/// Immutable trail of a COD transaction (سجل حركة مبلغ التحصيل): one row per event, with the status before and
/// after, the amount involved and the document that caused it. Append-only.
/// </summary>
public class CODTransactionHistory : BaseEntity, IAppendOnlyEntity
{
    public long CODTransactionId { get; set; }

    public CODTransaction CODTransaction { get; set; } = null!;

    public CODTransactionType TransactionType { get; set; }

    public CODStatus? FromStatus { get; set; }

    public CODStatus ToStatus { get; set; }

    /// <summary>The amount of the event (collected, handed over, settled, fee...). 0 when not relevant.</summary>
    public decimal Amount { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public long? DriverId { get; set; }

    public long? CashboxId { get; set; }

    /// <summary>"CashReceiptVoucher", "SenderSettlement", "CODAdjustment", "Shipment"...</summary>
    public string? ReferenceType { get; set; }

    public long? ReferenceId { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? Notes { get; set; }
}
