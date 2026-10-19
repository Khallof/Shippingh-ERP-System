using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Application.DTOs.Reports;
using ShippingERP.Application.Interfaces;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Application.Interfaces.Cod;
using ShippingERP.Application.Interfaces.Reports;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Domain.Shipments;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.Cash;
using ShippingERP.Infrastructure.Persistence;
using ShippingERP.Infrastructure.Shipments;

namespace ShippingERP.Infrastructure.Reporting;

/// <summary>
/// The management dashboard (step 8). It reuses the services of the earlier steps (COD dashboard, driver balances,
/// cashboxes, profit and loss) so every figure equals the one of its detailed screen, and adds the shipment counters.
/// Visibility: the shipment rules of ShipmentAccessService (branch / driver), the branch rules of each service, and
/// one permission per section (a section the caller may not see is null).
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private static readonly ShipmentStatus[] FinalStatuses =
    {
        ShipmentStatus.Delivered, ShipmentStatus.Returned, ShipmentStatus.Cancelled, ShipmentStatus.LostOrDamaged
    };

    private static readonly ShipmentStatus[] FailedStatuses =
    {
        ShipmentStatus.DeliveryFailed, ShipmentStatus.Returned, ShipmentStatus.LostOrDamaged
    };

    private readonly AppDbContext _dbContext;
    private readonly ShipmentAccessService _shipmentAccess;
    private readonly BillingAccess _billing;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly BusinessCalendar _calendar;
    private readonly ICodReportService _codReports;
    private readonly ICashboxService _cashboxes;
    private readonly IFinancialReportService _financialReports;

    public DashboardService(
        AppDbContext dbContext,
        ShipmentAccessService shipmentAccess,
        BillingAccess billing,
        ICurrentUserService currentUser,
        IDateTimeProvider clock,
        BusinessCalendar calendar,
        ICodReportService codReports,
        ICashboxService cashboxes,
        IFinancialReportService financialReports)
    {
        _dbContext = dbContext;
        _shipmentAccess = shipmentAccess;
        _billing = billing;
        _currentUser = currentUser;
        _clock = clock;
        _calendar = calendar;
        _codReports = codReports;
        _cashboxes = cashboxes;
        _financialReports = financialReports;
    }

    /// <summary>Anonymous calls (background jobs, tests) see everything; signed-in users need the permission.</summary>
    private bool Can(string permission) => !_currentUser.IsAuthenticated || _currentUser.HasPermission(permission);

    public async Task<DashboardDto> GetAsync(DashboardQuery query, CancellationToken cancellationToken)
    {
        if (query.BranchId.HasValue)
        {
            _shipmentAccess.BranchAccess.EnsureCanAccess(query.BranchId.Value);
        }

        var now = _clock.UtcNow;
        var today = _calendar.BusinessDate(now);
        var days = Math.Clamp(query.Days, 1, 365);

        DashboardOperationsDto? operations = null;
        IReadOnlyList<DashboardTopCustomerDto>? topCustomers = null;
        IReadOnlyList<DashboardActivityDto>? activity = null;
        if (Can(Permissions.Shipments.View))
        {
            var shipments = await ShipmentsAsync(query.BranchId, cancellationToken);
            operations = await OperationsAsync(shipments, today, days, cancellationToken);
            topCustomers = await TopCustomersAsync(shipments, today, days, cancellationToken);
            activity = await ActivityAsync(shipments, cancellationToken);
        }

        CodDashboardDto? cod = null;
        IReadOnlyList<DashboardDriverBalanceDto>? drivers = null;
        if (Can(Permissions.Cod.View))
        {
            cod = await _codReports.GetDashboardAsync(new CodDashboardQuery { BranchId = query.BranchId }, cancellationToken);
            var balances = await _codReports.GetDriverBalancesAsync(
                new DriverBalanceQuery { BranchId = query.BranchId, PageSize = 5, SortBy = "balance", SortDirection = "desc", OnlyOutstanding = true },
                cancellationToken);
            drivers = balances.Items.Select(x => new DashboardDriverBalanceDto
            {
                DriverId = x.DriverId,
                DriverName = x.DriverName,
                BranchName = x.BranchName,
                CurrencyCode = x.CurrencyCode,
                Balance = x.Balance,
                UnhandedCodCount = x.UnhandedCodCount
            }).ToList();
        }

        IReadOnlyList<DashboardCashboxDto>? cashboxes = null;
        if (Can(Permissions.Cashboxes.View))
        {
            var page = await _cashboxes.GetPagedAsync(
                new CashboxListQuery { BranchId = query.BranchId, IsActive = true, PageSize = 50, SortBy = "code" }, cancellationToken);
            cashboxes = page.Items.Select(x => new DashboardCashboxDto
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.ArabicName,
                BranchName = x.BranchName,
                CurrencyCode = x.CurrencyCode,
                Balance = x.CurrentBalance
            }).ToList();
        }

        DashboardFinanceDto? finance = null;
        var canFinance = Can(Permissions.Reports.ViewFinancial);
        var canInvoices = canFinance || Can(Permissions.Invoices.View);
        if (canFinance || canInvoices)
        {
            finance = await FinanceAsync(query.BranchId, today, now, canFinance, cancellationToken);
        }

        return new DashboardDto
        {
            AsOfUtc = now,
            Today = today,
            PeriodDays = days,
            BranchId = query.BranchId,
            Operations = operations,
            Cod = cod,
            Cashboxes = cashboxes,
            Finance = finance,
            DriverBalances = drivers,
            TopCustomers = topCustomers,
            RecentActivity = activity
        };
    }

    private async Task<IQueryable<Shipment>> ShipmentsAsync(long? branchId, CancellationToken cancellationToken)
    {
        var shipments = await _shipmentAccess.ScopeAsync(_dbContext.Shipments.AsNoTracking(), cancellationToken);
        if (branchId.HasValue)
        {
            var branch = branchId.Value;
            shipments = shipments.Where(x => x.OriginBranchId == branch || x.CurrentBranchId == branch || x.DestinationBranchId == branch);
        }

        return shipments;
    }

    private async Task<DashboardOperationsDto> OperationsAsync(IQueryable<Shipment> shipments, DateTime today, int days, CancellationToken ct)
    {
        var (todayStart, todayEnd) = _calendar.DayRangeUtc(today);
        var periodStart = _calendar.DayRangeUtc(today.AddDays(1 - days)).StartUtc;
        var weekStart = _calendar.DayRangeUtc(today.AddDays(-6)).StartUtc;

        var byStatus = await shipments
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var todayCreated = await shipments.CountAsync(x => x.CreatedAtUtc >= todayStart && x.CreatedAtUtc < todayEnd, ct);
        var todayDelivered = await shipments.CountAsync(x => x.DeliveredAtUtc >= todayStart && x.DeliveredAtUtc < todayEnd, ct);
        var todayFailed = await shipments
            .SelectMany(x => x.StatusHistory)
            .CountAsync(h => h.ToStatus == ShipmentStatus.DeliveryFailed && h.OccurredAtUtc >= todayStart && h.OccurredAtUtc < todayEnd, ct);

        var period = shipments.Where(x => x.CreatedAtUtc >= periodStart && x.Status != ShipmentStatus.Cancelled);
        var periodTotal = await period.CountAsync(ct);
        var periodDelivered = await period.CountAsync(x => x.Status == ShipmentStatus.Delivered, ct);
        var periodFailed = await period.CountAsync(x => FailedStatuses.Contains(x.Status), ct);

        var created = await shipments.Where(x => x.CreatedAtUtc >= weekStart).Select(x => x.CreatedAtUtc).ToListAsync(ct);
        var delivered = await shipments.Where(x => x.DeliveredAtUtc >= weekStart).Select(x => x.DeliveredAtUtc!.Value).ToListAsync(ct);
        var last7 = Enumerable.Range(0, 7)
            .Select(offset => today.AddDays(offset - 6))
            .Select(date => new DailyShipmentCountDto
            {
                Date = date,
                Created = created.Count(x => _calendar.BusinessDate(x) == date),
                Delivered = delivered.Count(x => _calendar.BusinessDate(x) == date)
            })
            .ToList();

        return new DashboardOperationsDto
        {
            TodayCreated = todayCreated,
            TodayDelivered = todayDelivered,
            TodayFailed = todayFailed,
            OpenShipments = byStatus.Where(x => !FinalStatuses.Contains(x.Status)).Sum(x => x.Count),
            OutForDelivery = byStatus.Where(x => x.Status == ShipmentStatus.OutForDelivery).Sum(x => x.Count),
            PeriodShipments = periodTotal,
            PeriodDelivered = periodDelivered,
            PeriodFailed = periodFailed,
            DeliveredPercent = Percent(periodDelivered, periodTotal),
            FailedPercent = Percent(periodFailed, periodTotal),
            ByStatus = byStatus
                .OrderBy(x => x.Status)
                .Select(x => new StatusCountDto { Status = x.Status, StatusName = ShipmentStatusNames.Arabic(x.Status), Count = x.Count })
                .ToList(),
            Last7Days = last7
        };
    }

    private async Task<IReadOnlyList<DashboardTopCustomerDto>> TopCustomersAsync(IQueryable<Shipment> shipments, DateTime today, int days, CancellationToken ct)
    {
        var periodStart = _calendar.DayRangeUtc(today.AddDays(1 - days)).StartUtc;
        var top = await shipments
            .Where(x => x.CreatedAtUtc >= periodStart && x.Status != ShipmentStatus.Cancelled)
            .GroupBy(x => x.SenderCustomerId)
            .Select(g => new { CustomerId = g.Key, Count = g.Count(), Charges = g.Sum(x => x.TotalCharge), Cod = g.Sum(x => x.CODAmount) })
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.Charges)
            .Take(5)
            .ToListAsync(ct);

        var ids = top.Select(x => x.CustomerId).ToList();
        var names = await _dbContext.Customers.AsNoTracking().IgnoreQueryFilters()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => new { x.Code, x.ArabicName }, ct);

        return top.Select(x => new DashboardTopCustomerDto
        {
            CustomerId = x.CustomerId,
            Code = names.TryGetValue(x.CustomerId, out var n) ? n.Code : "-",
            Name = names.TryGetValue(x.CustomerId, out var m) ? m.ArabicName : "-",
            ShipmentCount = x.Count,
            TotalCharges = x.Charges,
            CodAmount = x.Cod
        }).ToList();
    }

    private async Task<IReadOnlyList<DashboardActivityDto>> ActivityAsync(IQueryable<Shipment> shipments, CancellationToken ct)
    {
        var rows = await shipments
            .SelectMany(s => s.StatusHistory.Select(h => new
            {
                h.Id,
                h.OccurredAtUtc,
                h.ToStatus,
                h.BranchId,
                h.Notes,
                ShipmentId = s.Id,
                s.ShipmentNumber,
                s.TrackingNumber
            }))
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(10)
            .ToListAsync(ct);

        var branchIds = rows.Where(x => x.BranchId.HasValue).Select(x => x.BranchId!.Value).Distinct().ToList();
        var branches = await _dbContext.Branches.AsNoTracking().IgnoreQueryFilters()
            .Where(x => branchIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.ArabicName, ct);

        return rows.Select(x => new DashboardActivityDto
        {
            OccurredAtUtc = x.OccurredAtUtc,
            ShipmentId = x.ShipmentId,
            ShipmentNumber = x.ShipmentNumber,
            TrackingNumber = x.TrackingNumber,
            Status = x.ToStatus,
            StatusName = ShipmentStatusNames.Arabic(x.ToStatus),
            BranchName = x.BranchId.HasValue && branches.TryGetValue(x.BranchId.Value, out var name) ? name : null,
            Notes = x.Notes
        }).ToList();
    }

    private async Task<DashboardFinanceDto> FinanceAsync(long? branchId, DateTime today, DateTime now, bool withProfit, CancellationToken ct)
    {
        var baseCurrency = await _billing.GetBaseCurrencyAsync(ct);
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        decimal revenue = 0m, expenses = 0m, net = 0m;
        if (withProfit)
        {
            var pl = await _financialReports.GetProfitLossAsync(new ProfitLossQuery { FromDate = monthStart, ToDate = today, BranchId = branchId }, ct);
            revenue = pl.TotalRevenue;
            expenses = pl.TotalCostOfRevenue + pl.TotalExpenses;
            net = pl.NetProfit;
        }

        var open = _billing.Invoices()
            .Where(x => x.CurrencyId == baseCurrency.Id &&
                        (x.Status == InvoiceStatus.Issued || x.Status == InvoiceStatus.PartiallyPaid || x.Status == InvoiceStatus.Overdue) &&
                        x.BalanceDue > 0m);
        if (branchId.HasValue)
        {
            var branch = branchId.Value;
            open = open.Where(x => x.BranchId == branch);
        }

        var overdue = open.Where(x => x.DueDateUtc != null && x.DueDateUtc < now);

        return new DashboardFinanceDto
        {
            FromDate = monthStart,
            ToDate = today,
            BaseCurrencyCode = baseCurrency.Code,
            TotalRevenue = revenue,
            TotalExpenses = expenses,
            NetProfit = net,
            OverdueInvoicesCount = await overdue.CountAsync(ct),
            OverdueInvoicesAmount = await overdue.SumAsync(x => x.BalanceDue, ct),
            ReceivablesBalance = await open.SumAsync(x => x.BalanceDue, ct)
        };
    }

    private static decimal Percent(int part, int total) =>
        total == 0 ? 0m : Math.Round(part * 100m / total, 2, MidpointRounding.AwayFromZero);
}
