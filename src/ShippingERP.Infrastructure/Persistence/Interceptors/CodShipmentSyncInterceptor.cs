using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Cod;
using ShippingERP.Domain.Entities.Cod;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Keeps the CODTransaction of every COD shipment in step with the shipment, inside the SAME SaveChanges (and so
/// the same database transaction) as the shipment change. This covers every place that creates or moves a
/// shipment (create, Excel import, edit, cancel, driver manifests, delivery, failed delivery, returns,
/// corrections) without each of them having to remember it.
///
/// It only touches CODs whose money was not collected yet (Pending / AssignedToDriver / Cancelled):
///   - new COD shipment                -> CODTransaction Pending (or AssignedToDriver), history "Registered"
///   - out for delivery with a driver  -> AssignedToDriver
///   - back at a branch / failed       -> Pending
///   - COD amount edited               -> ExpectedAmount updated (history "ExpectedAmountChanged")
///   - cancelled, returned, COD removed-> Cancelled; restored later -> Reopened
/// Once money is involved, only the COD services change the record.
///
/// Registered BEFORE AuditableEntityInterceptor so the rows it adds get their audit columns.
/// </summary>
public sealed class CodShipmentSyncInterceptor : SaveChangesInterceptor
{
    private static readonly string[] WatchedProperties =
    {
        nameof(Shipment.Status), nameof(Shipment.IsCOD), nameof(Shipment.CODAmount), nameof(Shipment.AssignedDriverId),
        nameof(Shipment.CurrencyId), nameof(Shipment.SenderCustomerId), nameof(Shipment.OriginBranchId)
    };

    private readonly IDateTimeProvider _dateTimeProvider;

    public CodShipmentSyncInterceptor(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            var shipments = ChangedShipments(context);
            if (shipments.Count > 0)
            {
                var ids = PersistedIds(context, shipments);
                var loaded = ids.Count == 0
                    ? new List<CODTransaction>()
                    : context.Set<CODTransaction>().Where(x => ids.Contains(x.ShipmentId)).ToList();
                Apply(context, shipments, loaded);
            }
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            var shipments = ChangedShipments(context);
            if (shipments.Count > 0)
            {
                var ids = PersistedIds(context, shipments);
                var loaded = ids.Count == 0
                    ? new List<CODTransaction>()
                    : await context.Set<CODTransaction>().Where(x => ids.Contains(x.ShipmentId)).ToListAsync(cancellationToken);
                Apply(context, shipments, loaded);
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static List<EntityEntry<Shipment>> ChangedShipments(DbContext context) =>
        context.ChangeTracker.Entries<Shipment>()
            .Where(entry => entry.State == EntityState.Added ||
                            (entry.State == EntityState.Modified && WatchedProperties.Any(p => entry.Property(p).IsModified)))
            .ToList();

    private static List<long> PersistedIds(DbContext context, IEnumerable<EntityEntry<Shipment>> entries)
    {
        var tracked = context.ChangeTracker.Entries<CODTransaction>().Select(x => x.Entity.ShipmentId).ToHashSet();
        return entries
            .Where(x => x.State != EntityState.Added && x.Entity.CodTransaction is null && !tracked.Contains(x.Entity.Id))
            .Select(x => x.Entity.Id)
            .ToList();
    }

    private void Apply(DbContext context, IReadOnlyList<EntityEntry<Shipment>> entries, IReadOnlyCollection<CODTransaction> loaded)
    {
        var byShipment = context.ChangeTracker.Entries<CODTransaction>()
            .Select(x => x.Entity)
            .Concat(loaded)
            .Where(x => x.ShipmentId > 0)
            .GroupBy(x => x.ShipmentId)
            .ToDictionary(x => x.Key, x => x.First());

        var now = _dateTimeProvider.UtcNow;
        foreach (var entry in entries)
        {
            var shipment = entry.Entity;
            var cod = shipment.CodTransaction;
            if (cod is null && entry.State != EntityState.Added)
            {
                byShipment.TryGetValue(shipment.Id, out cod);
            }

            Sync(context, shipment, cod, entry.State == EntityState.Added, now);
        }
    }

    private static void Sync(DbContext context, Shipment shipment, CODTransaction? cod, bool isNewShipment, DateTime now)
    {
        var active = shipment.IsCOD &&
                     shipment.CODAmount > 0m &&
                     shipment.Status is not (ShipmentStatus.Cancelled or ShipmentStatus.Returned);
        var hasDriver = shipment.AssignedDriverId.HasValue;

        if (cod is null)
        {
            if (!active)
            {
                return;
            }

            var initial = CodStatusRules.NotCollectedStatusFor(shipment.Status, hasDriver, CODStatus.Pending);
            cod = new CODTransaction
            {
                Shipment = shipment,
                SenderCustomerId = shipment.SenderCustomerId,
                OriginBranchId = shipment.OriginBranchId,
                CurrencyId = shipment.CurrencyId,
                ExpectedAmount = shipment.CODAmount,
                Status = CODStatus.Pending,
                DriverId = null
            };
            shipment.CodTransaction = cod;
            context.Add(cod);
            AddHistory(context, cod, shipment, isNewShipment, CODTransactionType.Registered, null, CODStatus.Pending, shipment.CODAmount, now, "تسجيل مبلغ التحصيل مع الشحنة");

            if (initial == CODStatus.AssignedToDriver)
            {
                Move(context, cod, shipment, isNewShipment, CODStatus.AssignedToDriver, CODTransactionType.AssignedToDriver, now, null);
                cod.DriverId = shipment.AssignedDriverId;
            }

            return;
        }

        // Money is involved: only the COD workflows may change the record.
        if (!CodStatusRules.IsNotCollected(cod.Status))
        {
            return;
        }

        if (!active)
        {
            if (cod.Status != CODStatus.Cancelled)
            {
                var reason = !shipment.IsCOD || shipment.CODAmount <= 0m ? "أُلغي الدفع عند الاستلام من الشحنة" : "أُلغيت الشحنة أو أعيدت للمرسل قبل التحصيل";
                Move(context, cod, shipment, isNewShipment, CODStatus.Cancelled, CODTransactionType.Cancelled, now, reason);
                cod.CancelledAtUtc = now;
                cod.DriverId = null;
            }

            return;
        }

        if (cod.Status == CODStatus.Cancelled)
        {
            Move(context, cod, shipment, isNewShipment, CODStatus.Pending, CODTransactionType.Reopened, now, "أعيد تفعيل مبلغ التحصيل");
            cod.CancelledAtUtc = null;
        }

        if (cod.ExpectedAmount != shipment.CODAmount || cod.CurrencyId != shipment.CurrencyId ||
            cod.SenderCustomerId != shipment.SenderCustomerId || cod.OriginBranchId != shipment.OriginBranchId)
        {
            var amountChanged = cod.ExpectedAmount != shipment.CODAmount;
            cod.ExpectedAmount = shipment.CODAmount;
            cod.CurrencyId = shipment.CurrencyId;
            cod.SenderCustomerId = shipment.SenderCustomerId;
            cod.OriginBranchId = shipment.OriginBranchId;

            if (amountChanged)
            {
                AddHistory(context, cod, shipment, isNewShipment, CODTransactionType.ExpectedAmountChanged, cod.Status, cod.Status, shipment.CODAmount, now, "تعديل مبلغ التحصيل في الشحنة");
            }
        }

        var target = CodStatusRules.NotCollectedStatusFor(shipment.Status, hasDriver, cod.Status);
        if (target == CODStatus.AssignedToDriver)
        {
            if (cod.Status != CODStatus.AssignedToDriver || cod.DriverId != shipment.AssignedDriverId)
            {
                Move(context, cod, shipment, isNewShipment, CODStatus.AssignedToDriver, CODTransactionType.AssignedToDriver, now, null);
            }

            cod.DriverId = shipment.AssignedDriverId;
        }
        else if (target == CODStatus.Pending && cod.Status != CODStatus.Pending)
        {
            Move(context, cod, shipment, isNewShipment, CODStatus.Pending, CODTransactionType.Unassigned, now, null);
            cod.DriverId = null;
        }
    }

    private static void Move(
        DbContext context,
        CODTransaction cod,
        Shipment shipment,
        bool isNewShipment,
        CODStatus to,
        CODTransactionType type,
        DateTime now,
        string? notes)
    {
        var from = cod.Status;
        cod.Status = to;
        AddHistory(context, cod, shipment, isNewShipment, type, from, to, type == CODTransactionType.AssignedToDriver ? cod.ExpectedAmount : 0m, now, notes);
    }

    private static void AddHistory(
        DbContext context,
        CODTransaction cod,
        Shipment shipment,
        bool isNewShipment,
        CODTransactionType type,
        CODStatus? from,
        CODStatus to,
        decimal amount,
        DateTime now,
        string? notes)
    {
        context.Add(new CODTransactionHistory
        {
            CODTransaction = cod,
            TransactionType = type,
            FromStatus = from,
            ToStatus = to,
            Amount = amount,
            OccurredAtUtc = now,
            DriverId = type == CODTransactionType.AssignedToDriver ? shipment.AssignedDriverId : null,
            ReferenceType = "Shipment",
            ReferenceId = isNewShipment ? null : shipment.Id,
            ReferenceNumber = string.IsNullOrEmpty(shipment.ShipmentNumber) ? null : shipment.ShipmentNumber,
            Notes = notes
        });
    }
}
