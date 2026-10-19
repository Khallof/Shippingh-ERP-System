using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Cod;
using ShippingERP.Domain.Entities.Cod;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Infrastructure.Persistence.Seeding;

/// <summary>
/// Creates the missing CODTransaction rows of COD shipments that existed before step 6 (runs at every start,
/// demo data or not, and does nothing when every COD shipment already has its row). New shipments get their row
/// automatically from CodShipmentSyncInterceptor.
///
/// Delivered shipments are created as Pending: their money was never recorded in the system, so it is recorded
/// with POST /cod/collect (driver or counter) like any other delivered COD shipment.
/// </summary>
public sealed class CodBackfillSeeder
{
    private const int BatchSize = 500;

    private readonly AppDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CodBackfillSeeder> _logger;

    public CodBackfillSeeder(AppDbContext dbContext, IDateTimeProvider dateTimeProvider, ILogger<CodBackfillSeeder> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.IsRelational() &&
            (await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            _logger.LogWarning("COD backfill skipped: there are pending migrations");
            return 0;
        }

        var created = 0;
        while (true)
        {
            var shipments = await _dbContext.Shipments.AsNoTracking()
                .Where(x => x.IsCOD &&
                            x.CODAmount > 0m &&
                            x.Status != ShipmentStatus.Cancelled &&
                            x.Status != ShipmentStatus.Returned &&
                            x.CodTransaction == null)
                .OrderBy(x => x.Id)
                .Take(BatchSize)
                .Select(x => new
                {
                    x.Id,
                    x.ShipmentNumber,
                    x.SenderCustomerId,
                    x.OriginBranchId,
                    x.CurrencyId,
                    x.CODAmount,
                    x.Status,
                    x.AssignedDriverId
                })
                .ToListAsync(cancellationToken);

            if (shipments.Count == 0)
            {
                break;
            }

            var now = _dateTimeProvider.UtcNow;
            foreach (var shipment in shipments)
            {
                var status = CodStatusRules.NotCollectedStatusFor(shipment.Status, shipment.AssignedDriverId.HasValue, CODStatus.Pending);
                var cod = new CODTransaction
                {
                    ShipmentId = shipment.Id,
                    SenderCustomerId = shipment.SenderCustomerId,
                    OriginBranchId = shipment.OriginBranchId,
                    CurrencyId = shipment.CurrencyId,
                    ExpectedAmount = shipment.CODAmount,
                    Status = status,
                    DriverId = status == CODStatus.AssignedToDriver ? shipment.AssignedDriverId : null
                };

                _dbContext.CODTransactions.Add(cod);
                _dbContext.CODTransactionHistory.Add(new CODTransactionHistory
                {
                    CODTransaction = cod,
                    TransactionType = CODTransactionType.Registered,
                    FromStatus = null,
                    ToStatus = status,
                    Amount = shipment.CODAmount,
                    OccurredAtUtc = now,
                    DriverId = cod.DriverId,
                    ReferenceType = "Shipment",
                    ReferenceId = shipment.Id,
                    ReferenceNumber = shipment.ShipmentNumber,
                    Notes = "إنشاء تلقائي لشحنة دفع عند الاستلام سابقة للخطوة 6"
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            created += shipments.Count;
        }

        if (created > 0)
        {
            _logger.LogInformation("COD backfill: {Count} COD record(s) created for existing shipments", created);
        }

        return created;
    }
}
