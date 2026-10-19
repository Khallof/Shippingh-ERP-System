using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Application.Interfaces.Cod;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Cod;

/// <summary>COD reports and dashboard: driver balances, driver movements, sender balances, aging, dashboard, period reports.</summary>
public sealed class CodReportTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        _ids = await CodTestData.SeedAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<T> Reports<T>(Func<ICodReportService, Task<T>> action) => _context.UseAsync(action);

    [Fact]
    public async Task DriverBalances_ShowWhatEachDriverHolds_AndTheCodNotHandedOver()
    {
        await CodTestData.CollectedCodAsync(_context, _ids);
        await CodTestData.CollectedCodAsync(_context, _ids, cod: 40000m);

        var balances = await Reports(s => s.GetDriverBalancesAsync(new DriverBalanceQuery(), CancellationToken.None));

        var ali = Assert.Single(balances.Items);
        Assert.Equal(_ids.DamascusDriver, ali.DriverId);
        Assert.Equal(140000m, ali.Balance);
        Assert.Equal(2, ali.UnhandedCodCount);
        Assert.Equal(140000m, ali.UnhandedCodAmount);
        Assert.NotNull(ali.OldestUnhandedCollectionAtUtc);

        var movements = await Reports(s => s.GetDriverMovementsAsync(_ids.DamascusDriver, new DriverMovementQuery(), CancellationToken.None));
        Assert.Equal(2, movements.TotalCount);
        Assert.Equal(140000m, movements.Items[0].BalanceAfter);
    }

    [Fact]
    public async Task DriverUser_SeesOnlyHisOwnBalance()
    {
        await _context.SeedRolesAndPermissionsAsync();
        var other = await CodTestData.CreateDamascusDriverAsync(_context, _ids);
        await CodTestData.CollectedCodAsync(_context, _ids);
        await CodTestData.CollectedCodAsync(_context, _ids, cod: 30000m, driverId: other);
        var userId = await ShipmentTestData.LinkDriverUserAsync(_context, _ids.DamascusDriver, "driver.balance", _ids.Damascus);

        _context.CurrentUser.SignInWithRole(userId, _ids.Damascus, Roles.Driver, Permissions.Cod.View);
        var balances = await Reports(s => s.GetDriverBalancesAsync(new DriverBalanceQuery(), CancellationToken.None));

        Assert.Equal(_ids.DamascusDriver, Assert.Single(balances.Items).DriverId);
        await Assert.ThrowsAsync<Application.Common.Exceptions.NotFoundException>(() =>
            Reports(s => s.GetDriverMovementsAsync(other, new DriverMovementQuery(), CancellationToken.None)));
    }

    [Fact]
    public async Task SenderBalances_SplitTheHeldMoneyByStage()
    {
        await CodTestData.DeliveredCodShipmentAsync(_context, _ids, cod: 20000m);       // pending (delivered, not collected)
        await CodTestData.CollectedCodAsync(_context, _ids, cod: 30000m);               // with the driver
        await CodTestData.ReadyCodAsync(_context, _ids);                                // ready: net 82,000

        var balances = await Reports(s => s.GetSenderBalancesAsync(new SenderBalanceQuery(), CancellationToken.None));

        var sender = Assert.Single(balances.Items);
        Assert.Equal(_ids.Customer("CUS-00003"), sender.SenderCustomerId);
        Assert.Equal(1, sender.PendingCount);
        Assert.Equal(20000m, sender.PendingAmount);
        Assert.Equal(30000m, sender.WithDriversAmount);
        Assert.Equal(CodTestData.Net, sender.ReadyNetAmount);
        Assert.Equal(30000m + CodTestData.Cod, sender.TotalHeldAmount);
    }

    [Fact]
    public async Task Aging_PutsOutstandingCodInAgeBuckets()
    {
        await CodTestData.CollectedCodAsync(_context, _ids, cod: 50000m);
        _context.Clock.Advance(TimeSpan.FromDays(10));
        await CodTestData.CollectedCodAsync(_context, _ids, cod: 30000m);
        _context.Clock.Advance(TimeSpan.FromDays(1));

        var report = await Reports(s => s.GetAgingAsync(new CodAgingQuery(), CancellationToken.None));

        var row = Assert.Single(report.Rows);
        Assert.Equal(30000m, row.Days0To7);
        Assert.Equal(50000m, row.Days8To15);
        Assert.Equal(80000m, row.Total);
        Assert.Equal(80000m, row.WithDrivers);
        Assert.Equal(80000m, report.Totals.Total);
        Assert.Equal("SYP", report.CurrencyCode);
    }

    [Fact]
    public async Task Dashboard_CountsEveryStage()
    {
        await CodTestData.CodShipmentAtBranchAsync(_context, _ids, cod: 10000m);       // pending, not delivered
        await CodTestData.DeliveredCodShipmentAsync(_context, _ids, cod: 20000m);      // delivered, not collected
        await CodTestData.CollectedCodAsync(_context, _ids, cod: 30000m);              // with the driver
        await CodTestData.ReadyCodAsync(_context, _ids);                               // ready

        var dashboard = await Reports(s => s.GetDashboardAsync(new CodDashboardQuery(), CancellationToken.None));

        Assert.Equal(1, dashboard.Stages.Single(x => x.Status == CODStatus.Pending).Count);
        Assert.Equal(1, dashboard.Stages.Single(x => x.Status == CODStatus.AssignedToDriver).Count);
        Assert.Equal(30000m, dashboard.Stages.Single(x => x.Status == CODStatus.CollectedByDriver).Amount);
        Assert.Equal(CodTestData.Net, dashboard.Stages.Single(x => x.Status == CODStatus.ReadyForSenderSettlement).Amount);
        Assert.Equal(1, dashboard.DeliveredNotCollectedCount);
        Assert.Equal(20000m, dashboard.DeliveredNotCollectedAmount);
        Assert.Equal(1, dashboard.DriversWithBalance);
        Assert.Equal(30000m, dashboard.DriversOutstandingTotal);
        Assert.Equal(CodTestData.Net, dashboard.UnsettledSenderNet);
        Assert.Equal(2, dashboard.CashboxCount); // DAM-SYP-01 and ALP-SYP-01 (DAM-USD-01 is in another currency)
        Assert.Equal(500000m + 250000m + CodTestData.Cod, dashboard.CashboxBalanceTotal);
    }

    [Fact]
    public async Task DriverCollectionReport_ShowsCollectionsHandoversAndBalances()
    {
        await CodTestData.CollectedCodAsync(_context, _ids);
        await CodTestData.CollectedCodAsync(_context, _ids, cod: 40000m);
        await CodTestData.HandoverAsync(_context, _ids.DamascusDriver, 120000m);

        var report = await Reports(s => s.GetDriverCollectionReportAsync(new DriverCollectionReportQuery(), CancellationToken.None));

        var row = Assert.Single(report.Rows);
        Assert.Equal(0m, row.OpeningBalance);
        Assert.Equal(2, row.CollectedCount);
        Assert.Equal(140000m, row.CollectedAmount);
        Assert.Equal(1, row.HandoverCount);
        Assert.Equal(120000m, row.HandedOverAmount);
        Assert.Equal(20000m, row.ClosingBalance);
    }

    [Fact]
    public async Task SenderSettlementReport_TotalsThePaidSettlements()
    {
        var ready = await CodTestData.ReadyCodAsync(_context, _ids);
        var draft = await CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId);
        await CodTestData.ApproveSettlementAsync(_context, draft.Id);
        await CodTestData.PayCashAsync(_context, draft.Id);

        var report = await Reports(s => s.GetSenderSettlementReportAsync(new SenderSettlementReportQuery(), CancellationToken.None));

        Assert.Equal(1, report.SettlementCount);
        Assert.Equal(1, report.ShipmentCount);
        Assert.Equal(CodTestData.Cod, report.TotalGrossReleased);
        Assert.Equal(15000m, report.TotalShippingCharges);
        Assert.Equal(3000m, report.TotalCodCommission);
        Assert.Equal(CodTestData.Net, report.TotalNetPaid);
        Assert.Equal(SettlementStatus.Paid, Assert.Single(report.Rows).Status);
    }
}
