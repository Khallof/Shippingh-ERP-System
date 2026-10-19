using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;
using A = ShippingERP.Tests.Common.AccountingTestData;

namespace ShippingERP.Tests.Unit.Accounting;

/// <summary>
/// Fiscal years and accounting periods (step 7): monthly periods, no overlap, closing blocked while work is left,
/// no posting into a closed period, reopening, closing and deleting a year.
/// </summary>
public sealed class FiscalPeriodTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DateTime _today;
    private long _rent;
    private long _banks;

    public async Task InitializeAsync()
    {
        await A.SeedAsync(_context);
        _today = await A.TodayAsync(_context);
        _rent = await A.AccountIdAsync(_context, A.Rent);
        _banks = await A.AccountIdAsync(_context, A.Banks);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<T> Use<T>(Func<IFiscalPeriodService, Task<T>> action) => _context.UseAsync(action);

    private SaveJournalEntryRequest RentEntry(DateTime date) =>
        A.Entry(date, "إيجار", A.Debit(_rent, 50_000m), A.Credit(_banks, 50_000m));

    private DateTime NextJanuary(int day = 1) => new(_today.Year + 1, 1, day, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Seeder_CreatesTheCurrentCalendarYear_WithTwelveOpenPeriods()
    {
        var years = await Use(s => s.GetFiscalYearsAsync(CancellationToken.None));

        var year = Assert.Single(years, x => x.Code == $"FY{_today.Year}");
        Assert.Equal(12, year.PeriodCount);
        Assert.Equal(12, year.OpenPeriodCount);
        Assert.Equal(FiscalYearStatus.Open, year.Status);
        Assert.Equal(new DateTime(_today.Year, 1, 1), year.StartDate);
        Assert.Equal(new DateTime(_today.Year, 12, 31), year.EndDate);

        // Running the seeder again adds nothing.
        Assert.Equal(0, await A.SeedChartAsync(_context));
    }

    [Fact]
    public async Task OverlappingFiscalYear_IsRefused()
    {
        await Assert.ThrowsAsync<ConflictException>(() => Use(s => s.CreateFiscalYearAsync(
            new CreateFiscalYearRequest { Code = "FY-OVERLAP", StartDate = new DateTime(_today.Year, 7, 1, 0, 0, 0, DateTimeKind.Utc) },
            CancellationToken.None)));
    }

    [Fact]
    public async Task ClosedPeriod_RefusesEntries_UntilItIsReopened()
    {
        var next = await A.NextFiscalYearAsync(_context);
        var january = next.Periods.Single(x => x.PeriodNumber == 1);
        await A.ProcessAsync(_context); // a period closes only when every earlier operational event is posted

        var closed = await A.ClosePeriodAsync(_context, january.Id);
        Assert.Equal(AccountingPeriodStatus.Closed, closed.Status);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => A.CreateAsync(_context, RentEntry(NextJanuary(15))));
        Assert.Contains("مقفلة", error.Message);

        var reopened = await Use(s => s.ReopenPeriodAsync(january.Id, new ReopenAccountingPeriodRequest { Reason = "قيد تسوية متأخر من المدقق" }, CancellationToken.None));
        Assert.Equal(AccountingPeriodStatus.Open, reopened.Status);
        Assert.Equal("قيد تسوية متأخر من المدقق", reopened.ReopenReason);

        var created = await A.CreateAsync(_context, RentEntry(NextJanuary(15)));
        Assert.Equal(january.Id, (await A.GetAsync(_context, created.Entry.Id)).AccountingPeriodId);
    }

    [Fact]
    public async Task ApprovedEntry_CannotBePostedOnceItsPeriodIsClosed()
    {
        await A.NextFiscalYearAsync(_context);
        _context.CurrentUser.SignInAsSystemAdmin(801);
        var request = RentEntry(NextJanuary(10));
        request.Submit = true;
        var entry = (await A.CreateAsync(_context, request)).Entry;
        _context.CurrentUser.SignInAsSystemAdmin(802);
        await A.ApproveAsync(_context, entry.Id);

        // Simulates a period closed by a concurrent request after the approval.
        await _context.QueryAsync(async db =>
        {
            var period = await db.AccountingPeriods.SingleAsync(x => x.Id == entry.AccountingPeriodId);
            period.Status = AccountingPeriodStatus.Closed;
            return await db.SaveChangesAsync();
        });

        await Assert.ThrowsAsync<BusinessRuleException>(() => A.PostAsync(_context, entry.Id));
        Assert.Equal(JournalEntryStatus.Approved, (await A.GetAsync(_context, entry.Id)).Status);
        Assert.Equal(0m, await A.BalanceAsync(_context, A.Rent));
    }

    [Fact]
    public async Task PeriodClose_IsBlockedByDraftsAndWaitingEvents_ThenSucceeds()
    {
        var period = await A.CurrentPeriodAsync(_context, _today);
        var draft = (await A.CreateAsync(_context, RentEntry(_today))).Entry;

        var check = await Use(s => s.GetCloseCheckAsync(period.Id, CancellationToken.None));
        Assert.False(check.CanClose);
        Assert.Equal(1, check.UnpostedEntries);
        Assert.True(check.PendingPostingEvents >= 2);
        await Assert.ThrowsAsync<BusinessRuleException>(() => A.ClosePeriodAsync(_context, period.Id));

        await _context.UseAsync<IJournalEntryService>(s => s.DeleteAsync(draft.Id, CancellationToken.None));
        await A.ProcessAsync(_context);

        var ready = await Use(s => s.GetCloseCheckAsync(period.Id, CancellationToken.None));
        Assert.True(ready.CanClose);
        Assert.Empty(ready.Blockers);

        var closed = await A.ClosePeriodAsync(_context, period.Id);
        Assert.Equal(AccountingPeriodStatus.Closed, closed.Status);
        await Assert.ThrowsAsync<BusinessRuleException>(() => A.ClosePeriodAsync(_context, period.Id));
    }

    [Fact]
    public async Task FiscalYear_ClosesOnlyWhenEveryPeriodIsClosed_AndThenStaysClosed()
    {
        var next = await A.NextFiscalYearAsync(_context);
        await A.ProcessAsync(_context);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.CloseFiscalYearAsync(next.Id, CancellationToken.None)));

        foreach (var period in next.Periods)
        {
            await A.ClosePeriodAsync(_context, period.Id);
        }

        var closed = await Use(s => s.CloseFiscalYearAsync(next.Id, CancellationToken.None));
        Assert.Equal(FiscalYearStatus.Closed, closed.Status);
        Assert.Equal(0, closed.OpenPeriodCount);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.ReopenPeriodAsync(
            next.Periods[0].Id, new ReopenAccountingPeriodRequest { Reason = "محاولة إعادة فتح" }, CancellationToken.None)));
    }

    [Fact]
    public async Task FiscalYear_WithEntries_IsNotDeleted_AnEmptyOneIs()
    {
        await A.PostManualAsync(_context, RentEntry(_today));
        var current = (await Use(s => s.GetFiscalYearsAsync(CancellationToken.None))).Single(x => x.Code == $"FY{_today.Year}");
        var next = await A.NextFiscalYearAsync(_context);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.UseAsync<IFiscalPeriodService>(s => s.DeleteFiscalYearAsync(current.Id, CancellationToken.None)));
        await _context.UseAsync<IFiscalPeriodService>(s => s.DeleteFiscalYearAsync(next.Id, CancellationToken.None));

        Assert.False(await _context.QueryAsync(db => db.FiscalYears.AnyAsync(x => x.Id == next.Id)));
        Assert.False(await _context.QueryAsync(db => db.AccountingPeriods.AnyAsync(x => x.FiscalYearId == next.Id)));
    }

    [Fact]
    public async Task EntryOutsideAnyFiscalYear_IsRefused()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => A.CreateAsync(_context, RentEntry(NextJanuary(5).AddYears(5))));

        Assert.Contains("fiscal-years", error.Message);
    }
}
