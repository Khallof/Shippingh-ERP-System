using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.DTOs.Reports;
using ShippingERP.Application.Interfaces;
using ShippingERP.Application.Interfaces.Reports;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Domain.Shipments;
using ShippingERP.Infrastructure.Cash;
using ShippingERP.Infrastructure.Persistence;
using ShippingERP.Infrastructure.Shipments;

namespace ShippingERP.Infrastructure.Reporting;

/// <summary>
/// Operational report of the shipments CREATED in a period (step 8): totals, by status, branch (origin), driver,
/// sender and service type, and per day. Counts and sums are computed by SQL Server (GROUP BY); names are loaded after.
/// The visibility rules of the shipments apply (branch / driver).
/// </summary>
public sealed class OperationalReportService : IOperationalReportService
{
    private static readonly ShipmentStatus[] FailedStatuses =
    {
        ShipmentStatus.DeliveryFailed, ShipmentStatus.Returned, ShipmentStatus.LostOrDamaged
    };

    private static readonly ShipmentStatus[] DoneStatuses =
    {
        ShipmentStatus.Delivered, ShipmentStatus.DeliveryFailed, ShipmentStatus.Returned, ShipmentStatus.LostOrDamaged, ShipmentStatus.Cancelled
    };

    private readonly AppDbContext _dbContext;
    private readonly ShipmentAccessService _access;
    private readonly BusinessCalendar _calendar;
    private readonly IDateTimeProvider _clock;

    public OperationalReportService(AppDbContext dbContext, ShipmentAccessService access, BusinessCalendar calendar, IDateTimeProvider clock)
    {
        _dbContext = dbContext;
        _access = access;
        _calendar = calendar;
        _clock = clock;
    }

    private sealed class GroupTotals
    {
        public long? Key { get; init; }
        public int Total { get; init; }
        public int Delivered { get; init; }
        public int Failed { get; init; }
        public int Done { get; init; }
        public int Cancelled { get; init; }
        public decimal Charges { get; init; }
        public decimal Cod { get; init; }
    }

    public (DateTime From, DateTime To) Range(OperationalReportQuery query)
    {
        var today = _calendar.BusinessDate(_clock.UtcNow);
        var to = BusinessCalendar.AsDate(query.ToDate ?? today);
        var from = BusinessCalendar.AsDate(query.FromDate ?? to.AddDays(-29));
        return (from, to);
    }

    public async Task<IQueryable<Shipment>> FilteredAsync(OperationalReportQuery query, CancellationToken cancellationToken)
    {
        var (from, to) = Range(query);
        var startUtc = _calendar.DayRangeUtc(from).StartUtc;
        var endUtc = _calendar.DayRangeUtc(to).EndUtc;

        var shipments = await _access.ScopeAsync(_dbContext.Shipments.AsNoTracking(), cancellationToken);
        shipments = shipments.Where(x => x.CreatedAtUtc >= startUtc && x.CreatedAtUtc < endUtc);

        if (query.BranchId.HasValue)
        {
            _access.BranchAccess.EnsureCanAccess(query.BranchId.Value);
            var branch = query.BranchId.Value;
            shipments = shipments.Where(x => x.OriginBranchId == branch || x.CurrentBranchId == branch || x.DestinationBranchId == branch);
        }

        if (query.SenderCustomerId.HasValue)
        {
            var customer = query.SenderCustomerId.Value;
            shipments = shipments.Where(x => x.SenderCustomerId == customer);
        }

        if (query.DriverId.HasValue)
        {
            var driver = query.DriverId.Value;
            shipments = shipments.Where(x => x.AssignedDriverId == driver);
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            shipments = shipments.Where(x => x.Status == status);
        }

        return shipments;
    }

    public async Task<OperationalReportDto> GetAsync(OperationalReportQuery query, CancellationToken cancellationToken)
    {
        var (from, to) = Range(query);
        var shipments = await FilteredAsync(query, cancellationToken);

        var byBranch = await GroupAsync(shipments, x => (long?)x.OriginBranchId, cancellationToken);

        // Every shipment has exactly one origin branch: the branch groups add up to the totals.
        var totals = new GroupTotals
        {
            Total = byBranch.Sum(x => x.Total),
            Delivered = byBranch.Sum(x => x.Delivered),
            Failed = byBranch.Sum(x => x.Failed),
            Done = byBranch.Sum(x => x.Done),
            Cancelled = byBranch.Sum(x => x.Cancelled),
            Charges = byBranch.Sum(x => x.Charges),
            Cod = byBranch.Sum(x => x.Cod)
        };
        var byDriver = await GroupAsync(shipments, x => x.AssignedDriverId, cancellationToken);
        var byCustomer = await GroupAsync(shipments, x => (long?)x.SenderCustomerId, cancellationToken);
        var byService = await GroupAsync(shipments, x => (long?)x.ServiceTypeId, cancellationToken);

        var byStatus = await shipments
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var branchIds = byBranch.Where(x => x.Key.HasValue).Select(x => x.Key!.Value).ToList();
        var branchNames = await _dbContext.Branches.AsNoTracking().IgnoreQueryFilters()
            .Where(x => branchIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ArabicName, cancellationToken);

        var driverIds = byDriver.Where(x => x.Key.HasValue).Select(x => x.Key!.Value).ToList();
        var driverNames = await _dbContext.DriverProfiles.AsNoTracking().IgnoreQueryFilters()
            .Where(x => driverIds.Contains(x.Id))
            // Project first: ToDictionaryAsync runs its selectors in memory, where the Employee navigation is not loaded.
            .Select(x => new { x.Id, Name = x.Employee.ArabicName })
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var customerIds = byCustomer.Where(x => x.Key.HasValue).Select(x => x.Key!.Value).ToList();
        var customerNames = await _dbContext.Customers.AsNoTracking().IgnoreQueryFilters()
            .Where(x => customerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code + " - " + x.ArabicName, cancellationToken);

        var serviceIds = byService.Where(x => x.Key.HasValue).Select(x => x.Key!.Value).ToList();
        var serviceNames = await _dbContext.ServiceTypes.AsNoTracking().IgnoreQueryFilters()
            .Where(x => serviceIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ArabicName, cancellationToken);

        var created = await shipments.Select(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        var delivered = await shipments.Where(x => x.DeliveredAtUtc != null).Select(x => x.DeliveredAtUtc!.Value).ToListAsync(cancellationToken);
        var createdByDay = created.GroupBy(x => _calendar.BusinessDate(x)).ToDictionary(g => g.Key, g => g.Count());
        var deliveredByDay = delivered.GroupBy(x => _calendar.BusinessDate(x)).ToDictionary(g => g.Key, g => g.Count());

        var daily = new List<DailyShipmentCountDto>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            daily.Add(new DailyShipmentCountDto
            {
                Date = day,
                Created = createdByDay.GetValueOrDefault(day),
                Delivered = deliveredByDay.GetValueOrDefault(day)
            });
        }

        return new OperationalReportDto
        {
            FromDate = from,
            ToDate = to,
            Totals = Row(totals, null, "الإجمالي"),
            ByStatus = byStatus
                .OrderBy(x => x.Status)
                .Select(x => new StatusCountDto { Status = x.Status, StatusName = ShipmentStatusNames.Arabic(x.Status), Count = x.Count })
                .ToList(),
            ByBranch = Rows(byBranch, id => branchNames.GetValueOrDefault(id, "-"), "-"),
            ByDriver = Rows(byDriver, id => driverNames.GetValueOrDefault(id, "-"), "بدون سائق"),
            ByCustomer = Rows(byCustomer, id => customerNames.GetValueOrDefault(id, "-"), "-").Take(50).ToList(),
            ByServiceType = Rows(byService, id => serviceNames.GetValueOrDefault(id, "-"), "-"),
            Daily = daily
        };
    }

    private static async Task<List<GroupTotals>> GroupAsync(IQueryable<Shipment> shipments, Expression<Func<Shipment, long?>> key, CancellationToken ct) =>
        await shipments
            .GroupBy(key)
            .Select(g => new GroupTotals
            {
                Key = g.Key,
                Total = g.Count(),
                Delivered = g.Count(x => x.Status == ShipmentStatus.Delivered),
                Failed = g.Count(x => FailedStatuses.Contains(x.Status)),
                Done = g.Count(x => DoneStatuses.Contains(x.Status)),
                Cancelled = g.Count(x => x.Status == ShipmentStatus.Cancelled),
                Charges = g.Sum(x => x.TotalCharge),
                Cod = g.Sum(x => x.CODAmount)
            })
            .ToListAsync(ct);

    private static List<OperationalGroupRowDto> Rows(IEnumerable<GroupTotals> groups, Func<long, string> name, string emptyName) =>
        groups
            .Select(x => Row(x, x.Key, x.Key.HasValue ? name(x.Key.Value) : emptyName))
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.Name)
            .ToList();

    private static OperationalGroupRowDto Row(GroupTotals totals, long? id, string name)
    {
        var effective = totals.Total - totals.Cancelled;
        return new OperationalGroupRowDto
        {
            Id = id,
            Name = name,
            Total = totals.Total,
            Delivered = totals.Delivered,
            Failed = totals.Failed,
            Cancelled = totals.Cancelled,
            InProgress = totals.Total - totals.Done,
            DeliveredPercent = effective <= 0 ? 0m : Math.Round(totals.Delivered * 100m / effective, 2, MidpointRounding.AwayFromZero),
            TotalCharges = totals.Charges,
            CodAmount = totals.Cod
        };
    }
}
