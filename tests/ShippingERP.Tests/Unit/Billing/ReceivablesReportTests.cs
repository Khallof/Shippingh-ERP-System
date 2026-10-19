using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Application.Interfaces.Billing;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Billing;

/// <summary>Customer account statement and customer aging report.</summary>
public sealed class ReceivablesReportTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        await _context.SeedDemoDataAsync();
        _ids = await ShipmentTestData.LoadIdsAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private long Sender => _ids.Customer("CUS-00003");

    private async Task<InvoiceDto> IssuedInvoiceAsync(decimal weightKg = 2.5m, int paymentTermsDays = 30)
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, weightKg: weightKg));
        return await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true, paymentTermsDays: paymentTermsDays);
    }

    private Task<CustomerStatementDto> Statement(CustomerStatementQuery query) =>
        _context.UseAsync<IReceivablesReportService, CustomerStatementDto>(s => s.GetStatementAsync(Sender, query, CancellationToken.None));

    private Task<CustomerAgingReportDto> Aging(CustomerAgingQuery query) =>
        _context.UseAsync<IReceivablesReportService, CustomerAgingReportDto>(s => s.GetCustomerAgingAsync(query, CancellationToken.None));

    [Fact]
    public async Task Statement_ShowsEveryDocumentWithARunningBalance_AcrossPages()
    {
        var a = await IssuedInvoiceAsync();                 // +15,000
        _context.Clock.Advance(TimeSpan.FromHours(1));
        var b = await IssuedInvoiceAsync(weightKg: 9m);     // +23,000
        _context.Clock.Advance(TimeSpan.FromHours(1));
        await BillingTestData.PayAsync(_context, Sender, 20000m, allocations: new[] { (a.Id, 15000m), (b.Id, 5000m) }); // -20,000
        _context.Clock.Advance(TimeSpan.FromHours(1));
        await _context.UseAsync<IInvoiceService, CreditNoteDto>(s => s.CreateCreditNoteAsync(b.Id, new CreateCreditNoteRequest
        {
            Reason = "خصم لاحق",
            Lines = new List<CreditNoteLineRequest> { new() { InvoiceLineId = b.Lines[0].Id, TaxableAmount = 3000m } }
        }, CancellationToken.None));                         // -3,000

        var all = await Statement(new CustomerStatementQuery());

        Assert.Equal(0m, all.OpeningBalance);
        Assert.Equal(new[] { 15000m, 38000m, 18000m, 15000m }, all.Lines.Items.Select(x => x.Balance).ToArray());
        Assert.Equal(38000m, all.TotalDebit);
        Assert.Equal(23000m, all.TotalCredit);
        Assert.Equal(15000m, all.ClosingBalance);
        Assert.Equal(1, all.OpenInvoiceCount);
        Assert.Equal(15000m, all.OpenInvoicesBalance);
        Assert.Equal(new[] { "فاتورة", "فاتورة", "سند قبض", "إشعار دائن" }, all.Lines.Items.Select(x => x.TransactionTypeName).ToArray());

        // Page 2 of size 2 continues the running balance of page 1.
        var page2 = await Statement(new CustomerStatementQuery { PageNumber = 2, PageSize = 2 });
        Assert.Equal(new[] { 18000m, 15000m }, page2.Lines.Items.Select(x => x.Balance).ToArray());
        Assert.Equal(4, page2.Lines.TotalCount);
    }

    [Fact]
    public async Task Statement_WithAPeriod_StartsFromTheOpeningBalance()
    {
        await IssuedInvoiceAsync();
        _context.Clock.Advance(TimeSpan.FromDays(2));
        var from = _context.Clock.UtcNow.AddMinutes(-1);
        await IssuedInvoiceAsync(weightKg: 9m);

        var statement = await Statement(new CustomerStatementQuery { FromUtc = from });

        Assert.Equal(15000m, statement.OpeningBalance);
        var line = Assert.Single(statement.Lines.Items);
        Assert.Equal(38000m, line.Balance);
        Assert.Equal(38000m, statement.ClosingBalance);
    }

    [Fact]
    public async Task Aging_PutsEachOpenBalanceInTheBucketOfItsDaysPastDue()
    {
        var start = _context.Clock.UtcNow;
        var old = await IssuedInvoiceAsync(paymentTermsDays: 0);          // due at start
        _context.Clock.Advance(TimeSpan.FromDays(40));
        var recent = await IssuedInvoiceAsync(weightKg: 9m, paymentTermsDays: 30); // due at start + 70 days
        _context.Clock.Advance(TimeSpan.FromDays(5));
        await BillingTestData.PayAsync(_context, Sender, 6000m, allocations: new[] { (old.Id, 5000m) }); // 1,000 unallocated

        var today = await Aging(new CustomerAgingQuery());
        var row = Assert.Single(today.Customers.Items);

        Assert.Equal(10000m, row.Days31To60);   // old: 45 days past due, 15,000 - 5,000
        Assert.Equal(23000m, row.Current);      // recent: not due yet
        Assert.Equal(10000m, row.Overdue);
        Assert.Equal(33000m, row.TotalDue);
        Assert.Equal(1000m, row.UnappliedCredits);
        Assert.Equal(32000m, row.NetBalance);
        Assert.Equal(2, row.OpenInvoiceCount);
        Assert.Equal(45, row.OldestDaysPastDue);
        Assert.Equal(33000m, today.Totals.TotalDue);
        Assert.Equal("SYP", today.CurrencyCode);

        // As of day 40 the payment did not exist yet: the full 15,000 is 31-60 days old.
        var past = await Aging(new CustomerAgingQuery { AsOfUtc = start.AddDays(40) });
        var pastRow = Assert.Single(past.Customers.Items);
        Assert.Equal(15000m, pastRow.Days31To60);
        Assert.Equal(23000m, pastRow.Current);
        Assert.Equal(0m, pastRow.UnappliedCredits);

        // As of day 100 everything is overdue: old 100 days (90+), recent 30 days (1-30).
        var future = await Aging(new CustomerAgingQuery { AsOfUtc = start.AddDays(100) });
        var futureRow = Assert.Single(future.Customers.Items);
        Assert.Equal(10000m, futureRow.Days90Plus);
        Assert.Equal(23000m, futureRow.Days1To30);
    }

    [Fact]
    public async Task Aging_IgnoresCancelledAndPaidInvoices_AndRespectsTheBranchScope()
    {
        var paid = await IssuedInvoiceAsync();
        await BillingTestData.PayAsync(_context, Sender, 15000m, allocations: new[] { (paid.Id, 15000m) });
        var cancelled = await IssuedInvoiceAsync();
        await _context.UseAsync<IInvoiceService, InvoiceDto>(s => s.CancelAsync(cancelled.Id, new CancelInvoiceRequest { Reason = "فاتورة خاطئة" }, CancellationToken.None));
        await IssuedInvoiceAsync(weightKg: 9m);

        var report = await Aging(new CustomerAgingQuery());
        Assert.Equal(23000m, Assert.Single(report.Customers.Items).TotalDue);

        // An Aleppo user does not see the Damascus invoices.
        _context.CurrentUser.SignInAs(40, _ids.Aleppo, Permissions.Reports.ViewFinancial);
        var aleppo = await Aging(new CustomerAgingQuery());
        Assert.Empty(aleppo.Customers.Items);
        Assert.Equal(0m, aleppo.Totals.TotalDue);
    }

    [Fact]
    public async Task StatementExport_IsAnExcelFile()
    {
        await IssuedInvoiceAsync();

        var file = await _context.UseAsync<IReceivablesReportService, Application.Common.Files.FileDownload>(s =>
            s.ExportStatementAsync(Sender, new CustomerStatementQuery(), CancellationToken.None));

        Assert.EndsWith(".xlsx", file.FileName);
        Assert.True(file.Content.Length > 0);
        await file.Content.DisposeAsync();
    }

    [Fact]
    public async Task AccountTransactions_AreAppendOnly()
    {
        await IssuedInvoiceAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _context.QueryAsync(async db =>
        {
            var row = db.CustomerAccountTransactions.Single();
            row.Debit = 1m;
            return await db.SaveChangesAsync();
        }));

        Assert.Equal(CustomerAccountTransactionType.InvoiceIssued, await _context.QueryAsync(db => Task.FromResult(db.CustomerAccountTransactions.Single().TransactionType)));
    }
}
