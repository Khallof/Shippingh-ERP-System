using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Accounting;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Persistence.Seeding;
using ShippingERP.Tests.Common;
using A = ShippingERP.Tests.Common.AccountingTestData;

namespace ShippingERP.Tests.Unit.Accounting;

/// <summary>Chart of accounts and automatic posting rules (step 7): seeded chart, account tree rules, rule resolution.</summary>
public sealed class ChartOfAccountsTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        _ids = await A.SeedAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<T> Chart<T>(Func<IChartOfAccountsService, Task<T>> action) => _context.UseAsync(action);

    private Task<T> Rules<T>(Func<IPostingRuleService, Task<T>> action) => _context.UseAsync(action);

    private async Task<SaveAccountRequest> ChildOfAsync(string parentCode, string code, AccountType type, string name = "حساب فرعي جديد") => new()
    {
        Code = code,
        ArabicName = name,
        AccountType = type,
        ParentAccountId = await A.AccountIdAsync(_context, parentCode),
        IsPostable = true,
        AllowManualPosting = true,
        IsActive = true
    };

    [Fact]
    public async Task SeededChart_HasTheSixTypes_ConsistentSigns_AndARuleForEveryRole()
    {
        var accounts = await _context.QueryAsync(db => db.Accounts.ToListAsync());

        Assert.Equal(6, accounts.Where(x => x.Level == 1).Select(x => x.AccountType).Distinct().Count());
        Assert.All(accounts, x => Assert.Equal(AccountRules.NormalBalanceOf(x.AccountType), x.NormalBalance));
        Assert.All(accounts.Where(x => x.Level == 1), x => Assert.False(x.IsPostable));
        Assert.All(accounts, x => Assert.True(x.IsSystem));

        foreach (var code in new[] { A.Cashboxes, A.Banks, A.Receivables, A.DriverCustody, A.SenderCod, "2103", A.Suppliers, A.TaxPayable,
                     A.ShippingRevenue, A.CodCommission, "4103", A.Fuel, A.Salaries, A.Rent, "6102", "6107", A.Capital, A.RetainedEarnings })
        {
            Assert.Contains(accounts, x => x.Code == code && x.IsPostable);
        }

        var rules = await _context.QueryAsync(db => db.AccountingPostingRules.Where(x => x.IsActive).ToListAsync());
        Assert.All(AccountRoles.All, role => Assert.Contains(rules, x => x.AccountRole == role && PostingRuleMatcher.Specificity(x) == 0));
        Assert.Equal(4, await _context.QueryAsync(db => db.AccountingDimensions.CountAsync(x => x.DimensionType == DimensionType.CostCenter)));
    }

    [Fact]
    public async Task ExpenseRule_OfTheCategory_BeatsTheGeneralExpenseRule()
    {
        var fuelCategory = await _context.QueryAsync(db => db.ExpenseCategories.Where(x => x.Code == "FUEL").Select(x => x.Id).SingleAsync());

        var fuel = await Rules(s => s.ResolveAsync(new ResolvePostingAccountQuery { AccountRole = AccountRoles.Expense, ExpenseCategoryId = fuelCategory }, CancellationToken.None));
        var other = await Rules(s => s.ResolveAsync(new ResolvePostingAccountQuery { AccountRole = "expense" }, CancellationToken.None));

        Assert.Equal(A.Fuel, fuel.AccountCode);
        Assert.Equal(A.OtherExpenses, other.AccountCode);
        Assert.Equal(AccountRoles.Expense, other.AccountRole);
    }

    [Fact]
    public async Task CashboxSpecificRule_SendsThatCashboxToItsOwnAccount()
    {
        var aleppoCash = await Chart(async s => await s.CreateAccountAsync(await ChildOfAsync("11", "1105", AccountType.Assets, "صندوق حلب"), CancellationToken.None));
        var aleppoBox = await CodTestData.CashboxIdAsync(_context, "ALP-SYP-01");

        var rule = await Rules(s => s.CreateAsync(new SavePostingRuleRequest
        {
            AccountRole = AccountRoles.CashOnHand, AccountId = aleppoCash.Id, CashboxId = aleppoBox, IsActive = true, Description = "صندوق حلب في حسابه"
        }, CancellationToken.None));
        Assert.Equal(1, rule.Specificity);

        await A.ProcessAsync(_context);

        Assert.Equal(250_000m, await A.BalanceAsync(_context, "1105"));
        Assert.Equal(500_000m, await A.BalanceAsync(_context, A.Cashboxes));
    }

    [Fact]
    public async Task OnlyGeneralRuleOfARole_CannotBeDeleted()
    {
        var bankRule = await _context.QueryAsync(db => db.AccountingPostingRules.Where(x => x.AccountRole == AccountRoles.Bank).Select(x => x.Id).SingleAsync());

        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.UseAsync<IPostingRuleService>(s => s.DeleteAsync(bankRule, CancellationToken.None)));
    }

    [Fact]
    public async Task NewAccount_InheritsTheTypeLevelAndSignOfItsParent()
    {
        var created = await Chart(async s => await s.CreateAccountAsync(await ChildOfAsync("61", "6111", AccountType.Expenses, "مصاريف تغليف"), CancellationToken.None));

        Assert.Equal(3, created.Level);
        Assert.Equal(NormalBalance.Debit, created.NormalBalance);
        Assert.Equal("61", created.ParentAccountCode);
        Assert.False(created.IsSystem);

        await Assert.ThrowsAsync<ConflictException>(async () => await Chart(async s => await s.CreateAccountAsync(await ChildOfAsync("61", "6111", AccountType.Expenses), CancellationToken.None)));

        var wrongType = await Assert.ThrowsAsync<RequestValidationException>(async () =>
            await Chart(async s => await s.CreateAccountAsync(await ChildOfAsync("61", "6112", AccountType.Revenue), CancellationToken.None)));
        Assert.Contains("accountType", wrongType.Errors.Keys);

        var postableParent = await Assert.ThrowsAsync<RequestValidationException>(async () =>
            await Chart(async s => await s.CreateAccountAsync(await ChildOfAsync(A.Fuel, "61011", AccountType.Expenses), CancellationToken.None)));
        Assert.Contains("parentAccountId", postableParent.Errors.Keys);
    }

    [Fact]
    public async Task Accounts_WithEntriesOrSystemAccounts_AreNotDeleted()
    {
        var rent = await A.AccountIdAsync(_context, A.Rent);
        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.UseAsync<IChartOfAccountsService>(s => s.DeleteAccountAsync(rent, CancellationToken.None)));

        var packaging = await Chart(async s => await s.CreateAccountAsync(await ChildOfAsync("61", "6111", AccountType.Expenses, "مصاريف تغليف"), CancellationToken.None));
        var unused = await Chart(async s => await s.CreateAccountAsync(await ChildOfAsync("61", "6112", AccountType.Expenses, "حساب غير مستخدم"), CancellationToken.None));
        var today = await A.TodayAsync(_context);
        var banks = await A.AccountIdAsync(_context, A.Banks);
        await A.PostManualAsync(_context, A.Entry(today, "مواد تغليف", A.Debit(packaging.Id, 7_000m), A.Credit(banks, 7_000m)));

        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.UseAsync<IChartOfAccountsService>(s => s.DeleteAccountAsync(packaging.Id, CancellationToken.None)));
        await _context.UseAsync<IChartOfAccountsService>(s => s.DeleteAccountAsync(unused.Id, CancellationToken.None));

        Assert.False(await _context.QueryAsync(db => db.Accounts.AnyAsync(x => x.Id == unused.Id)));
    }

    [Fact]
    public async Task DemoSeeder_PostsTheWaitingEvents_FourManualEntries_AndLeavesOneForApproval()
    {
        var created = await _context.UseAsync<DemoAccountingSeeder, int>(s => s.SeedAsync(force: true));

        Assert.Equal(5, created);
        var manual = await _context.QueryAsync(db => db.JournalEntries.Where(x => !x.IsSystemGenerated).Select(x => x.Status).ToListAsync());
        Assert.Equal(4, manual.Count(x => x == JournalEntryStatus.Posted));
        Assert.Equal(1, manual.Count(x => x == JournalEntryStatus.Submitted));
        Assert.Equal(0, await _context.QueryAsync(db => db.OutboxMessages.CountAsync(x => x.ProcessedAtUtc == null)));
        Assert.Equal(35_000_000m, -await A.BalanceAsync(_context, A.Capital));

        var trial = await _context.UseAsync<IFinancialReportService, TrialBalanceDto>(s => s.GetTrialBalanceAsync(new TrialBalanceQuery(), CancellationToken.None));
        Assert.True(trial.IsBalanced);

        // Safe to run again: manual entries exist, nothing is added.
        Assert.Equal(0, await _context.UseAsync<DemoAccountingSeeder, int>(s => s.SeedAsync(force: true)));
    }

    [Fact]
    public async Task Tree_ShowsHeadersWithTheirChildren_AndRolledUpBalances()
    {
        await A.ProcessAsync(_context);

        var tree = await Chart(s => s.GetTreeAsync(new AccountTreeQuery { IncludeBalances = true }, CancellationToken.None));

        var assets = Assert.Single(tree, x => x.Code == "1");
        Assert.Equal(6, tree.Count);
        var current = Assert.Single(assets.Children, x => x.Code == "11");
        Assert.Contains(current.Children, x => x.Code == A.Cashboxes && x.Balance == 750_000m);
        Assert.Equal(750_000m, assets.Balance);
    }
}
