using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Entities.Accounting;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Accounting;
using ShippingERP.Infrastructure.Persistence.Seeding;

namespace ShippingERP.Tests.Common;

/// <summary>
/// Helpers of the accounting tests (step 7). The default chart of accounts (ChartOfAccountsSeeder):
///   1101 cashboxes, 1103 banks, 1104 customer cash awaiting deposit, 1111 receivables, 1112 driver COD custody,
///   2101 suppliers, 2102 sender COD liability, 2104 tax payable, 2105 accrued expenses, 3100 owner capital,
///   3200 retained earnings, 3300 opening balances, 4101 shipping revenue, 4102 COD commission, 6101 fuel,
///   6201 salaries, 6202 rent, 6205 bank charges, 6209 other expenses.
/// </summary>
public static class AccountingTestData
{
    public const string Cashboxes = "1101";
    public const string Banks = "1103";
    public const string CashClearing = "1104";
    public const string Receivables = "1111";
    public const string DriverCustody = "1112";
    public const string Vehicles = "1201";
    public const string Suppliers = "2101";
    public const string SenderCod = "2102";
    public const string TaxPayable = "2104";
    public const string Accrued = "2105";
    public const string Capital = "3100";
    public const string RetainedEarnings = "3200";
    public const string OpeningBalances = "3300";
    public const string ShippingRevenue = "4101";
    public const string CodCommission = "4102";
    public const string OtherIncome = "4201";
    public const string Fuel = "6101";
    public const string Salaries = "6201";
    public const string Rent = "6202";
    public const string BankCharges = "6205";
    public const string OtherExpenses = "6209";

    /// <summary>
    /// Demo master data, the demo cashboxes (DAM-SYP-01 500,000 and ALP-SYP-01 250,000: two opening-balance events in
    /// the outbox) and the chart of accounts with its posting rules, cost centers and the current fiscal year.
    /// </summary>
    public static async Task<DemoIds> SeedAsync(IdentityTestContext context, bool withCashboxes = true)
    {
        await context.SeedDemoDataAsync();
        if (withCashboxes)
        {
            await context.UseAsync<DemoCashSeeder, int>(s => s.SeedAsync(force: true));
        }

        await SeedChartAsync(context);
        return await ShipmentTestData.LoadIdsAsync(context);
    }

    public static Task<int> SeedChartAsync(IdentityTestContext context) =>
        context.UseAsync<ChartOfAccountsSeeder, int>(s => s.RunAsync(CancellationToken.None));

    public static Task<long> AccountIdAsync(IdentityTestContext context, string code) =>
        context.QueryAsync(db => db.Accounts.Where(x => x.Code == code).Select(x => x.Id).SingleAsync());

    public static Task<long> CostCenterIdAsync(IdentityTestContext context, string code) =>
        context.QueryAsync(db => db.AccountingDimensions.Where(x => x.DimensionType == DimensionType.CostCenter && x.Code == code).Select(x => x.Id).SingleAsync());

    /// <summary>Today's business date as the accounting module sees it.</summary>
    public static Task<DateTime> TodayAsync(IdentityTestContext context) =>
        context.UseAsync<AccountingAccess, DateTime>(access => Task.FromResult(access.Today));

    /// <summary>Posts every waiting outbox message now (what the background dispatcher does).</summary>
    public static Task<ProcessPostingsResultDto> ProcessAsync(IdentityTestContext context) =>
        context.UseAsync<IAccountingPostingService, ProcessPostingsResultDto>(s => s.ProcessPendingAsync(1000, CancellationToken.None));

    public static JournalLineRequest Debit(long accountId, decimal amount, long? costCenterId = null, long? supplierId = null, long? bankAccountId = null) =>
        new() { AccountId = accountId, Debit = amount, CostCenterId = costCenterId, SupplierId = supplierId, BankAccountId = bankAccountId };

    public static JournalLineRequest Credit(long accountId, decimal amount, long? costCenterId = null, long? supplierId = null, long? bankAccountId = null) =>
        new() { AccountId = accountId, Credit = amount, CostCenterId = costCenterId, SupplierId = supplierId, BankAccountId = bankAccountId };

    public static SaveJournalEntryRequest Entry(DateTime date, string description, params JournalLineRequest[] lines) =>
        new()
        {
            EntryDate = date,
            JournalType = JournalType.General,
            Description = description,
            Lines = lines.ToList()
        };

    public static Task<JournalCreationResult> CreateAsync(IdentityTestContext context, SaveJournalEntryRequest request, string? idempotencyKey = null) =>
        context.UseAsync<IJournalEntryService, JournalCreationResult>(s => s.CreateAsync(request, idempotencyKey, CancellationToken.None));

    public static Task<JournalEntryDto> GetAsync(IdentityTestContext context, long id) =>
        context.UseAsync<IJournalEntryService, JournalEntryDto>(s => s.GetByIdAsync(id, CancellationToken.None));

    public static Task<JournalEntryDto> SubmitAsync(IdentityTestContext context, long id) =>
        context.UseAsync<IJournalEntryService, JournalEntryDto>(s => s.SubmitAsync(id, CancellationToken.None));

    public static Task<JournalEntryDto> ApproveAsync(IdentityTestContext context, long id) =>
        context.UseAsync<IJournalEntryService, JournalEntryDto>(s => s.ApproveAsync(id, new JournalDecisionRequest(), CancellationToken.None));

    public static Task<JournalEntryDto> PostAsync(IdentityTestContext context, long id) =>
        context.UseAsync<IJournalEntryService, JournalEntryDto>(s => s.PostAsync(id, CancellationToken.None));

    public static Task<JournalEntryDto> ReverseAsync(IdentityTestContext context, long id, string reason = "تصحيح خطأ في القيد", DateTime? date = null) =>
        context.UseAsync<IJournalEntryService, JournalEntryDto>(s => s.ReverseAsync(id, new ReverseJournalEntryRequest { Reason = reason, ReversalDate = date }, CancellationToken.None));

    /// <summary>
    /// The full manual workflow with two different users (four eyes): user <paramref name="authorId"/> creates and
    /// submits, user <paramref name="approverId"/> approves and posts. The caller is signed out afterwards.
    /// </summary>
    public static async Task<JournalEntryDto> PostManualAsync(IdentityTestContext context, SaveJournalEntryRequest request, long authorId = 801, long approverId = 802)
    {
        context.CurrentUser.SignInAsSystemAdmin(authorId);
        request.Submit = true;
        var created = await CreateAsync(context, request);

        context.CurrentUser.SignInAsSystemAdmin(approverId);
        await ApproveAsync(context, created.Entry.Id);
        var posted = await PostAsync(context, created.Entry.Id);

        context.CurrentUser.SignOut();
        return posted;
    }

    /// <summary>Balance (debit − credit, base currency) of an account from the posted ledger lines.</summary>
    public static Task<decimal> BalanceAsync(IdentityTestContext context, string code) =>
        context.QueryAsync(async db =>
        {
            var lines = await db.JournalEntryLines
                .Where(x => x.Account.Code == code &&
                            (x.JournalEntry.Status == JournalEntryStatus.Posted || x.JournalEntry.Status == JournalEntryStatus.Reversed))
                .Select(x => new { x.DebitBase, x.CreditBase })
                .ToListAsync();
            return lines.Sum(x => x.DebitBase - x.CreditBase);
        });

    /// <summary>The automatic entries of one operational event type, with their lines and accounts.</summary>
    public static Task<List<JournalEntry>> EntriesOfAsync(IdentityTestContext context, string eventType) =>
        context.QueryAsync(db => db.JournalEntries
            .Include(x => x.Lines).ThenInclude(x => x.Account)
            .Where(x => x.SourceEventType == eventType)
            .OrderBy(x => x.Id)
            .ToListAsync());

    /// <summary>Debit − credit per account code of one entry (only the accounts it touches).</summary>
    public static Dictionary<string, decimal> NetByAccount(JournalEntry entry) =>
        entry.Lines
            .GroupBy(x => x.Account.Code)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Debit - x.Credit));

    /// <summary>Every posted entry (manual or automatic) must be balanced in its currency and in the base currency.</summary>
    public static Task<List<string>> UnbalancedEntriesAsync(IdentityTestContext context) =>
        context.QueryAsync(async db =>
        {
            var entries = await db.JournalEntries.Include(x => x.Lines).ToListAsync();
            return entries
                .Where(x => x.Lines.Sum(l => l.Debit) != x.Lines.Sum(l => l.Credit) ||
                            x.Lines.Sum(l => l.DebitBase) != x.Lines.Sum(l => l.CreditBase) ||
                            x.TotalDebit != x.Lines.Sum(l => l.Debit) ||
                            x.TotalCreditBase != x.Lines.Sum(l => l.CreditBase))
                .Select(x => $"{x.EntryNumber} ({x.SourceEventType}): {x.Lines.Sum(l => l.Debit)} <> {x.Lines.Sum(l => l.Credit)}")
                .ToList();
        });

    public static Task<AccountingPeriodDto> CurrentPeriodAsync(IdentityTestContext context, DateTime date) =>
        context.UseAsync<IFiscalPeriodService, AccountingPeriodDto>(async s =>
        {
            var page = await s.GetPeriodsAsync(new AccountingPeriodListQuery { Date = date }, CancellationToken.None);
            return Assert.Single(page.Items);
        });

    public static Task<AccountingPeriodDto> ClosePeriodAsync(IdentityTestContext context, long periodId) =>
        context.UseAsync<IFiscalPeriodService, AccountingPeriodDto>(s => s.ClosePeriodAsync(periodId, new CloseAccountingPeriodRequest { Notes = "إقفال تجريبي" }, CancellationToken.None));

    /// <summary>Creates the fiscal year after the current one (January to December) when it does not exist yet.</summary>
    public static async Task<FiscalYearDto> NextFiscalYearAsync(IdentityTestContext context)
    {
        var today = await TodayAsync(context);
        var start = new DateTime(today.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return await context.UseAsync<IFiscalPeriodService, FiscalYearDto>(s => s.CreateFiscalYearAsync(
            new CreateFiscalYearRequest { StartDate = start }, CancellationToken.None));
    }
}
