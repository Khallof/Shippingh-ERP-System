using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Shipments;

/// <summary>
/// One status change of a shipment (the tracking timeline). Append-only: it can never be updated or deleted
/// (AuditableEntityInterceptor refuses it). Mistakes are fixed with a new correction row.
/// CreatedByUserId = who made the change; CreatedAtUtc = when it was saved.
/// </summary>
public class ShipmentStatusHistory : BaseEntity, IAppendOnlyEntity
{
    public long ShipmentId { get; set; }

    public Shipment Shipment { get; set; } = null!;

    /// <summary>Null for the first row (creation).</summary>
    public ShipmentStatus? FromStatus { get; set; }

    public ShipmentStatus ToStatus { get; set; }

    public ShipmentStatusChangeSource Source { get; set; }

    /// <summary>When it happened (may be earlier than CreatedAtUtc, e.g. a driver app syncing later).</summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>The branch where the shipment was at that moment.</summary>
    public long? BranchId { get; set; }

    public long? WarehouseId { get; set; }

    public long? DriverId { get; set; }

    /// <summary>Reason or notes (required for cancellations, failed deliveries and corrections).</summary>
    public string? Notes { get; set; }

    /// <summary>The document that caused the change, e.g. "BranchTransfer".</summary>
    public string? ReferenceType { get; set; }

    public long? ReferenceId { get; set; }

    /// <summary>The document number, e.g. "BTR-2026-000004" (shown in the timeline).</summary>
    public string? ReferenceNumber { get; set; }

    public bool IsCorrection { get; set; }
}
