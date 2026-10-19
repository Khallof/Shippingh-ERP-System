using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Domain.Shipments;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Shipments;

/// <summary>Details of one status change (who / where / why / which document).</summary>
public sealed record StatusChange(ShipmentStatusChangeSource Source)
{
    /// <summary>When it happened (default: now). Cannot be in the future.</summary>
    public DateTime? OccurredAtUtc { get; init; }

    /// <summary>Required when the new status is InWarehouse.</summary>
    public long? WarehouseId { get; init; }

    public long? DriverId { get; init; }

    public string? Notes { get; init; }

    public string? ReferenceType { get; init; }

    public long? ReferenceId { get; init; }

    public string? ReferenceNumber { get; init; }
}

/// <summary>
/// The ONLY place that changes Shipment.Status. It checks the state machine (ShipmentStatusRules), updates the
/// milestone dates and the current warehouse, and adds the immutable ShipmentStatusHistory row.
/// It does not call SaveChanges: the caller saves everything in one transaction.
/// </summary>
public sealed class ShipmentStatusChanger
{
    /// <summary>Small tolerance for clocks of mobile devices.</summary>
    private static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ShipmentStatusChanger(AppDbContext dbContext, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>First history row of a new shipment (null -> Created).</summary>
    public ShipmentStatusHistory RecordCreation(Shipment shipment, ShipmentStatusChangeSource source, string? notes = null)
    {
        var history = new ShipmentStatusHistory
        {
            Shipment = shipment,
            FromStatus = null,
            ToStatus = ShipmentStatus.Created,
            Source = source,
            OccurredAtUtc = _dateTimeProvider.UtcNow,
            BranchId = shipment.CurrentBranchId,
            Notes = notes
        };

        shipment.StatusHistory.Add(history);
        return history;
    }

    /// <summary>True when the change is allowed (used to validate manifest lines before applying them).</summary>
    public static bool CanChange(Shipment shipment, ShipmentStatus to, ShipmentStatusChangeSource source) =>
        ShipmentStatusRules.CanTransition(shipment.Status, to, source);

    public ShipmentStatusHistory Change(Shipment shipment, ShipmentStatus to, StatusChange change)
    {
        var from = shipment.Status;

        if (!ShipmentStatusRules.CanTransition(from, to, change.Source))
        {
            throw new BusinessRuleException(TransitionError(shipment.ShipmentNumber, from, to));
        }

        var now = _dateTimeProvider.UtcNow;
        var occurredAt = change.OccurredAtUtc ?? now;
        if (occurredAt > now + FutureTolerance)
        {
            throw new BusinessRuleException("وقت تغيير الحالة لا يمكن أن يكون في المستقبل.");
        }

        if (to == ShipmentStatus.InWarehouse && change.WarehouseId is null)
        {
            throw new BusinessRuleException("يجب تحديد المستودع عند إدخال الشحنة إلى المستودع.");
        }

        shipment.Status = to;
        shipment.CurrentWarehouseId = to == ShipmentStatus.InWarehouse ? change.WarehouseId : null;

        switch (to)
        {
            case ShipmentStatus.PickedUp:
                shipment.PickedUpAtUtc ??= occurredAt;
                break;
            case ShipmentStatus.Delivered:
                shipment.DeliveredAtUtc = occurredAt;
                break;
            case ShipmentStatus.Returned:
                shipment.ReturnedAtUtc = occurredAt;
                break;
            case ShipmentStatus.Cancelled:
                shipment.CancelledAtUtc = occurredAt;
                break;
        }

        // A correction or a return out of a final status clears the milestone that is no longer true.
        if (from == ShipmentStatus.Delivered && to != ShipmentStatus.Delivered && change.Source == ShipmentStatusChangeSource.Correction)
        {
            shipment.DeliveredAtUtc = null;
        }

        if (from == ShipmentStatus.Returned && to != ShipmentStatus.Returned)
        {
            shipment.ReturnedAtUtc = null;
        }

        if (from == ShipmentStatus.Cancelled && to != ShipmentStatus.Cancelled)
        {
            shipment.CancelledAtUtc = null;
            shipment.CancellationReason = null;
        }

        var history = new ShipmentStatusHistory
        {
            Shipment = shipment,
            FromStatus = from,
            ToStatus = to,
            Source = change.Source,
            OccurredAtUtc = occurredAt,
            BranchId = shipment.CurrentBranchId,
            WarehouseId = shipment.CurrentWarehouseId,
            DriverId = change.DriverId ?? shipment.AssignedDriverId,
            Notes = Truncate(change.Notes, 500),
            ReferenceType = change.ReferenceType,
            ReferenceId = change.ReferenceId,
            ReferenceNumber = change.ReferenceNumber,
            IsCorrection = change.Source == ShipmentStatusChangeSource.Correction
        };

        _dbContext.ShipmentStatusHistory.Add(history);
        return history;
    }

    public static string TransitionError(string shipmentNumber, ShipmentStatus from, ShipmentStatus to) =>
        $"لا يمكن تغيير حالة الشحنة {shipmentNumber} من «{ShipmentStatusNames.Arabic(from)}» إلى «{ShipmentStatusNames.Arabic(to)}» بهذه العملية.";

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= maxLength ? value.Trim() : value.Trim()[..maxLength];
}
