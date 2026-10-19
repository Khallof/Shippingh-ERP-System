using System.Text;
using ClosedXML.Excel;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.DTOs.Reports;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Reports;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;
using A = ShippingERP.Tests.Common.AccountingTestData;

namespace ShippingERP.Tests.Unit.Reports;

/// <summary>
/// Dashboard, operational report, Excel / PDF exports and the audit log (step 8). Scenario: one shipment just
/// created and one COD shipment delivered by the Damascus driver (both from sender CUS-00003), the COD collected,
/// the cashbox opening balances posted to the ledger.
/// </summary>
public sealed class ReportingTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;
    private ShipmentDto _created = null!;
    private ShipmentDto _delivered = null!;
    private long _manifestId;

    public async Task InitializeAsync()
    {
        _ids = await A.SeedAsync(_context);
        _created = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));

        var cod = await CodTestData.CodShipmentAtBranchAsync(_context, _ids);
        _manifestId = await CodTestData.DispatchAsync(_context, _ids.DamascusDriver, cod);
        await CodTestData.DeliverAsync(_context, cod.Id);
        await CodTestData.CollectAsync(_context, cod.Id);
        _delivered = await ShipmentTestData.GetAsync(_context, cod.Id);

        await A.ProcessAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<T> Use<T>(Func<IReportExportService, Task<T>> action) => _context.UseAsync(action);

    private static byte[] Bytes(FileDownload file)
    {
        using var memory = new MemoryStream();
        file.Content.CopyTo(memory);
        return memory.ToArray();
    }

    private static void AssertPdf(FileDownload file)
    {
        Assert.Equal("application/pdf", file.ContentType);
        Assert.EndsWith(".pdf", file.FileName);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(Bytes(file), 0, 5));
    }

    private static XLWorkbook Workbook(FileDownload file)
    {
        Assert.EndsWith(".xlsx", file.FileName);
        return new XLWorkbook(new MemoryStream(Bytes(file)));
    }

    private static bool Contains(IXLWorksheet sheet, string text) =>
        sheet.CellsUsed().Any(cell => string.Equals(cell.GetString(), text, StringComparison.Ordinal));

    // ------------------------------------------------------------------ dashboard

    [Fact]
    public async Task Dashboard_CountsTodaysShipments_AndFillsEverySectionForAFullUser()
    {
        var dashboard = await _context.UseAsync<IDashboardService, DashboardDto>(s => s.GetAsync(new DashboardQuery(), CancellationToken.None));

        var operations = Assert.IsType<DashboardOperationsDto>(dashboard.Operations);
        Assert.Equal(2, operations.TodayCreated);
        Assert.Equal(1, operations.TodayDelivered);
        Assert.Equal(2, operations.PeriodShipments);
        Assert.Equal(1, operations.PeriodDelivered);
        Assert.Equal(50m, operations.DeliveredPercent);
        Assert.Equal(1, operations.OpenShipments);
        Assert.Contains(operations.ByStatus, x => x.Status == ShipmentStatus.Delivered && x.Count == 1);
        Assert.Equal(7, operations.Last7Days.Count);
        Assert.Equal(2, operations.Last7Days[^1].Created);

        Assert.NotNull(dashboard.Cod);
        Assert.Equal(3, dashboard.Cashboxes!.Count);
        Assert.Equal(750_000m, dashboard.Cashboxes.Where(x => x.CurrencyCode == "SYP").Sum(x => x.Balance));
        Assert.NotNull(dashboard.Finance);
        Assert.Equal(CodTestData.Cod, Assert.Single(dashboard.DriverBalances!).Balance);

        var top = Assert.Single(dashboard.TopCustomers!);
        Assert.Equal("CUS-00003", top.Code);
        Assert.Equal(2, top.ShipmentCount);
        Assert.Equal(CodTestData.Cod, top.CodAmount);

        Assert.NotEmpty(dashboard.RecentActivity!);
        Assert.Equal(_delivered.Id, dashboard.RecentActivity![0].ShipmentId);
    }

    [Fact]
    public async Task Dashboard_HidesTheSectionsTheUserMayNotSee()
    {
        _context.CurrentUser.SignInAs(900, null, Permissions.Shipments.View, Permissions.Branches.ViewAll);

        var dashboard = await _context.UseAsync<IDashboardService, DashboardDto>(s => s.GetAsync(new DashboardQuery(), CancellationToken.None));

        Assert.NotNull(dashboard.Operations);
        Assert.Null(dashboard.Cod);
        Assert.Null(dashboard.DriverBalances);
        Assert.Null(dashboard.Cashboxes);
        Assert.Null(dashboard.Finance);
    }

    [Fact]
    public async Task Dashboard_OfAnotherBranch_IsForbiddenToABranchUser()
    {
        _context.CurrentUser.SignInAs(901, _ids.Damascus, Permissions.Shipments.View);

        await Assert.ThrowsAsync<ShippingERP.Application.Common.Exceptions.ForbiddenException>(() =>
            _context.UseAsync<IDashboardService, DashboardDto>(s => s.GetAsync(new DashboardQuery { BranchId = _ids.Aleppo }, CancellationToken.None)));
    }

    // ------------------------------------------------------------------ operational report

    [Fact]
    public async Task OperationalReport_GroupsAddUpToTheTotals()
    {
        var report = await _context.UseAsync<IOperationalReportService, OperationalReportDto>(s => s.GetAsync(new OperationalReportQuery(), CancellationToken.None));

        Assert.Equal(2, report.Totals.Total);
        Assert.Equal(1, report.Totals.Delivered);
        Assert.Equal(1, report.Totals.InProgress);
        Assert.Equal(50m, report.Totals.DeliveredPercent);
        Assert.Equal(report.Totals.Total, report.ByBranch.Sum(x => x.Total));
        Assert.Equal(report.Totals.Total, report.ByDriver.Sum(x => x.Total));
        Assert.Equal(report.Totals.Total, report.ByStatus.Sum(x => x.Count));
        Assert.Equal(report.Totals.TotalCharges, report.ByCustomer.Sum(x => x.TotalCharges));
        Assert.Contains(report.ByDriver, x => x.Id == _ids.DamascusDriver && x.Delivered == 1);
        Assert.Contains(report.ByDriver, x => x.Id == null && x.Name == "بدون سائق");
        Assert.Equal(30, report.Daily.Count);
        Assert.Equal(2, report.Daily.Sum(x => x.Created));
        Assert.Equal(1, report.Daily.Sum(x => x.Delivered));
    }

    [Fact]
    public async Task OperationalReport_FiltersByDriverAndStatus()
    {
        var byDriver = await _context.UseAsync<IOperationalReportService, OperationalReportDto>(s =>
            s.GetAsync(new OperationalReportQuery { DriverId = _ids.DamascusDriver }, CancellationToken.None));
        var byStatus = await _context.UseAsync<IOperationalReportService, OperationalReportDto>(s =>
            s.GetAsync(new OperationalReportQuery { Status = ShipmentStatus.Created }, CancellationToken.None));

        Assert.Equal(1, byDriver.Totals.Total);
        Assert.Equal(1, byDriver.Totals.Delivered);
        Assert.Equal(1, byStatus.Totals.Total);
        Assert.Equal(0, byStatus.Totals.Delivered);
    }

    // ------------------------------------------------------------------ exports

    [Fact]
    public async Task ShipmentsExport_ContainsEveryFilteredShipment()
    {
        using var workbook = Workbook(await Use(s => s.ShipmentsAsync(new ShipmentListQuery(), ExportFormat.Excel, CancellationToken.None)));
        var sheet = workbook.Worksheets.First();

        Assert.True(sheet.RightToLeft);
        Assert.True(Contains(sheet, "قائمة الشحنات"));
        Assert.True(Contains(sheet, _created.TrackingNumber));
        Assert.True(Contains(sheet, _delivered.TrackingNumber));

        using var onlyCod = Workbook(await Use(s => s.ShipmentsAsync(new ShipmentListQuery { IsCOD = true }, ExportFormat.Excel, CancellationToken.None)));
        Assert.False(Contains(onlyCod.Worksheets.First(), _created.TrackingNumber));

        AssertPdf(await Use(s => s.ShipmentsAsync(new ShipmentListQuery(), ExportFormat.Pdf, CancellationToken.None)));
    }

    [Fact]
    public async Task TrialBalanceExport_HasTheAccountsAndBalancedTotals()
    {
        using var workbook = Workbook(await Use(s => s.TrialBalanceAsync(new TrialBalanceQuery(), ExportFormat.Excel, CancellationToken.None)));
        var sheet = workbook.Worksheets.First();

        Assert.True(Contains(sheet, "ميزان المراجعة"));
        Assert.True(Contains(sheet, A.Cashboxes));
        Assert.True(Contains(sheet, A.DriverCustody));

        var totals = sheet.CellsUsed().Single(cell => cell.GetString() == "الإجمالي").WorksheetRow();
        var closingDebit = totals.Cell(7).GetValue<decimal>();
        var closingCredit = totals.Cell(8).GetValue<decimal>();
        Assert.Equal(closingDebit, closingCredit);
        Assert.True(closingDebit > 0m);

        AssertPdf(await Use(s => s.TrialBalanceAsync(new TrialBalanceQuery(), ExportFormat.Pdf, CancellationToken.None)));
    }

    [Fact]
    public async Task GeneralLedger_ProfitLoss_AndBalanceSheet_Export()
    {
        var cashboxes = await A.AccountIdAsync(_context, A.Cashboxes);

        using var ledger = Workbook(await Use(s => s.GeneralLedgerAsync(new GeneralLedgerQuery { AccountId = cashboxes }, ExportFormat.Excel, CancellationToken.None)));
        Assert.True(Contains(ledger.Worksheets.First(), "الرصيد الافتتاحي"));
        AssertPdf(await Use(s => s.GeneralLedgerAsync(new GeneralLedgerQuery { AccountId = cashboxes }, ExportFormat.Pdf, CancellationToken.None)));

        AssertPdf(await Use(s => s.ProfitLossAsync(new ProfitLossQuery(), ExportFormat.Pdf, CancellationToken.None)));
        using var balance = Workbook(await Use(s => s.BalanceSheetAsync(new BalanceSheetQuery(), ExportFormat.Excel, CancellationToken.None)));
        Assert.True(Contains(balance.Worksheets.First(), "الميزانية العمومية"));
    }

    [Fact]
    public async Task OperationalReport_AndDeliveryManifest_ArePrintable()
    {
        AssertPdf(await Use(s => s.OperationalReportAsync(new OperationalReportQuery(), ExportFormat.Pdf, CancellationToken.None)));
        using var workbook = Workbook(await Use(s => s.OperationalReportAsync(new OperationalReportQuery(), ExportFormat.Excel, CancellationToken.None)));
        Assert.True(Contains(workbook.Worksheets.First(), "حسب السائق"));

        var manifest = await Use(s => s.DriverManifestPdfAsync(_manifestId, CancellationToken.None));
        AssertPdf(manifest);
        Assert.StartsWith("manifest-", manifest.FileName);
    }

    // ------------------------------------------------------------------ audit log

    [Fact]
    public async Task AuditLog_ListsTheRecordedActions_NewestFirst()
    {
        var page = await _context.UseAsync<IAuditLogQueryService, ShippingERP.Application.Common.Models.PagedResult<AuditLogDto>>(s =>
            s.GetPagedAsync(new AuditLogListQuery { PageSize = 100 }, CancellationToken.None));

        Assert.NotEmpty(page.Items);
        Assert.True(page.TotalCount >= page.Items.Count);
        Assert.All(page.Items, x => Assert.Equal(x.Action.ToString(), x.ActionName));
        Assert.True(page.Items.Zip(page.Items.Skip(1)).All(pair => pair.First.CreatedAtUtc >= pair.Second.CreatedAtUtc));

        var action = page.Items[0].Action;
        var filtered = await _context.UseAsync<IAuditLogQueryService, ShippingERP.Application.Common.Models.PagedResult<AuditLogDto>>(s =>
            s.GetPagedAsync(new AuditLogListQuery { Action = action }, CancellationToken.None));
        Assert.All(filtered.Items, x => Assert.Equal(action, x.Action));
    }
}
