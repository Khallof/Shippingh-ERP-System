using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Tests.Common;
using A = ShippingERP.Tests.Common.AccountingTestData;

namespace ShippingERP.Tests.Unit.Accounting;

/// <summary>
/// Financial statements reconcile with each other and with the sub-ledgers (step 7). One scenario for all tests:
///   - cashbox opening balances 750,000 (automatic)
///   - owner capital: bank 10,000,000 + vehicles 25,000,000 (manual)
///   - office rent 1,200,000 paid by bank, cost center ADMIN (manual)
///   - fuel supplier bill 450,000, cost center FLEET (manual)
///   - an invoice of 15,000 paid in cash (automatic)
///   - a COD of 100,000 collected, handed over and settled: sender net 82,000, revenue 15,000 + 3,000 (automatic)
///   - a fuel expense voucher of 25,000 from the cashbox (automatic)
/// Revenue 33,000, expenses 1,675,000: net loss 1,642,000.
/// </summary>
public sealed class FinancialReportTests : IAsyncLifetime
{
    private const decimal Revenue = 33_000m;
    private const decimal Expenses = 1_675_000m;

    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;
    private DateTime _today;
    private long _supplierId;
    private long _rentEntryId;

    public async Task InitializeAsync()
    {
        _ids = await A.SeedAsync(_context);
        _today = await A.TodayAsync(_context);
        _supplierId = await _context.QueryAsync(db => db.Suppliers.Where(x => x.Code == "SUP-001").Select(x => x.Id).SingleAsync());
        var bankAccountId = await CodTestData.BankAccountIdAsync(_context);

        var accounts = await _context.QueryAsync(db => db.Accounts.ToDictionaryAsync(x => x.Code, x => x.Id));
        long Id(string code) => accounts[code];
        var admin = await A.CostCenterIdAsync(_context, "ADMIN");
        var fleet = await A.CostCenterIdAsync(_context, "FLEET");

        var capital = A.Entry(_today, "رأس مال المالك",
            A.Debit(Id(A.Banks), 10_000_000m, bankAccountId: bankAccountId),
            A.Debit(Id(A.Vehicles), 25_000_000m),
            A.Credit(Id(A.Capital), 35_000_000m));
        capital.JournalType = Domain.Enums.JournalType.Opening;
        await A.PostManualAsync(_context, capital);

        _rentEntryId = (await A.PostManualAsync(_context, A.Entry(_today, "إيجار المكتب",
            A.Debit(Id(A.Rent), 1_200_000m, costCenterId: admin),
            A.Credit(Id(A.Banks), 1_200_000m, bankAccountId: bankAccountId)))).Id;

        await A.PostManualAsync(_context, A.Entry(_today, "فاتورة وقود آجلة",
            A.Debit(Id(A.Fuel), 450_000m, costCenterId: fleet),
            A.Credit(Id(A.Suppliers), 450_000m, supplierId: _supplierId)));

        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);
        await BillingTestData.PayAsync(_context, _ids.Customer("CUS-00003"), 15_000m, "CASH", allocations: new[] { (invoice.Id, 15_000m) });

        var cod = await CodTestData.ReadyCodAsync(_context, _ids);
        var settlement = await CodTestData.CreateSettlementAsync(_context, cod.SenderCustomerId, new SettlementLineRequest { CodTransactionId = cod.Id });
        await CodTestData.ApproveSettlementAsync(_context, settlement.Id);
        await CodTestData.PayCashAsync(_context, settlement.Id);

        await CodTestData.PayExpenseAsync(_context, await CodTestData.CashboxIdAsync(_context), 25_000m);

        var processed = await A.ProcessAsync(_context);
        Assert.Equal(0, processed.Failed);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<T> Reports<T>(Func<IFinancialReportService, Task<T>> action) => _context.UseAsync(action);

    [Fact]
    public async Task EveryPostedEntry_IsBalanced()
    {
        Assert.Empty(await A.UnbalancedEntriesAsync(_context));
    }

    [Fact]
    public async Task TrialBalance_IsBalanced_AtAccountAndGroupLevel()
    {
        var detail = await Reports(s => s.GetTrialBalanceAsync(new TrialBalanceQuery(), CancellationToken.None));

        Assert.True(detail.IsBalanced);
        Assert.Equal(detail.TotalClosingDebit, detail.TotalClosingCredit);
        Assert.Equal(detail.TotalPeriodDebit, detail.TotalPeriodCredit);
        var rent = Assert.Single(detail.Rows, x => x.Code == A.Rent);
        Assert.Equal(1_200_000m, rent.ClosingDebit);
        Assert.Equal(0m, rent.ClosingCredit);

        var top = await Reports(s => s.GetTrialBalanceAsync(new TrialBalanceQuery { Level = 1 }, CancellationToken.None));
        Assert.True(top.IsBalanced);
        Assert.All(top.Rows, x => Assert.Equal(1, x.Level));
        Assert.Equal(detail.TotalPeriodDebit, top.TotalPeriodDebit);
    }

    [Fact]
    public async Task OneCurrency_IsReportedInItsOwnAmounts_WithoutTheOthers()
    {
        var syp = await _context.QueryAsync(db => db.Currencies.Where(x => x.Code == "SYP").Select(x => x.Id).SingleAsync());
        var other = await _context.QueryAsync(db => db.Currencies.Where(x => x.Code != "SYP").Select(x => x.Id).FirstAsync());

        var pl = await Reports(s => s.GetProfitLossAsync(new ProfitLossQuery { CurrencyId = syp }, CancellationToken.None));
        Assert.Equal("SYP", pl.BaseCurrencyCode);
        Assert.Equal(Revenue, pl.TotalRevenue);

        var trial = await Reports(s => s.GetTrialBalanceAsync(new TrialBalanceQuery { CurrencyId = syp }, CancellationToken.None));
        Assert.True(trial.IsBalanced);

        var none = await Reports(s => s.GetProfitLossAsync(new ProfitLossQuery { CurrencyId = other }, CancellationToken.None));
        Assert.Equal(0m, none.TotalRevenue);
        Assert.Equal(0m, none.TotalExpenses);
    }

    [Fact]
    public async Task ProfitAndLoss_ShowsRevenueAndExpenses_WithoutTheCodAmount()
    {
        var pl = await Reports(s => s.GetProfitLossAsync(new ProfitLossQuery(), CancellationToken.None));

        Assert.Equal(Revenue, pl.TotalRevenue);
        Assert.Equal(0m, pl.TotalCostOfRevenue);
        Assert.Equal(Expenses, pl.TotalExpenses);
        Assert.Equal(Revenue - Expenses, pl.NetProfit);
        var revenueAccounts = pl.Revenue.SelectMany(x => x.Accounts).ToDictionary(x => x.Code, x => x.Amount);
        Assert.Equal(30_000m, revenueAccounts[A.ShippingRevenue]);
        Assert.Equal(3_000m, revenueAccounts[A.CodCommission]);
    }

    [Fact]
    public async Task BalanceSheet_Balances_AndItsYearProfitEqualsTheProfitAndLoss()
    {
        var pl = await Reports(s => s.GetProfitLossAsync(new ProfitLossQuery(), CancellationToken.None));
        var bs = await Reports(s => s.GetBalanceSheetAsync(new BalanceSheetQuery(), CancellationToken.None));

        Assert.True(bs.IsBalanced);
        Assert.Equal(bs.TotalAssets, bs.TotalLiabilitiesAndEquity);
        Assert.Equal(pl.NetProfit, bs.CurrentYearProfit);
        Assert.Equal(0m, bs.PriorYearsProfit);
        Assert.Equal(450_000m, bs.TotalLiabilities);
        Assert.Equal(35_000_000m + 750_000m + (Revenue - Expenses), bs.TotalEquity);
    }

    [Fact]
    public async Task CashFlow_Reconciles_WithTheCashAccounts()
    {
        var flow = await Reports(s => s.GetCashFlowAsync(new CashFlowQuery(), CancellationToken.None));

        Assert.True(flow.IsReconciled);
        Assert.Equal(0m, flow.OpeningCash);
        var cash = await A.BalanceAsync(_context, A.Cashboxes) + await A.BalanceAsync(_context, "1102") +
                   await A.BalanceAsync(_context, A.Banks) + await A.BalanceAsync(_context, A.CashClearing);
        Assert.Equal(cash, flow.ClosingCash);
        Assert.Equal(flow.ClosingCash - flow.OpeningCash, flow.NetChange);

        var investing = Assert.Single(flow.Sections, x => x.Activity == Domain.Enums.CashFlowActivity.Investing);
        Assert.Equal(25_000_000m, investing.TotalOutflow);
        var financing = Assert.Single(flow.Sections, x => x.Activity == Domain.Enums.CashFlowActivity.Financing);
        Assert.Equal(35_750_000m, financing.TotalInflow);
    }

    [Fact]
    public async Task GeneralLedger_RunningBalance_EndsOnTheTrialBalance()
    {
        var banks = await A.AccountIdAsync(_context, A.Banks);

        var ledger = await Reports(s => s.GetGeneralLedgerAsync(new GeneralLedgerQuery { AccountId = banks }, CancellationToken.None));
        var trial = await Reports(s => s.GetTrialBalanceAsync(new TrialBalanceQuery(), CancellationToken.None));

        Assert.Equal(8_800_000m, ledger.ClosingBalance);
        Assert.Equal(ledger.ClosingBalance, ledger.Lines.Items[^1].RunningBalance);
        Assert.Equal(ledger.OpeningBalance + ledger.PeriodDebit - ledger.PeriodCredit, ledger.ClosingBalance);
        var row = trial.Rows.Single(x => x.Code == A.Banks);
        Assert.Equal(ledger.ClosingBalance, row.ClosingDebit - row.ClosingCredit);
    }

    [Fact]
    public async Task GeneralLedger_OfAHeaderAccount_IncludesItsSubAccounts()
    {
        var expenses = await A.AccountIdAsync(_context, "6");

        var ledger = await Reports(s => s.GetGeneralLedgerAsync(new GeneralLedgerQuery { AccountId = expenses }, CancellationToken.None));

        Assert.True(ledger.IncludesSubAccounts);
        Assert.Equal(Expenses, ledger.ClosingBalance);
    }

    [Fact]
    public async Task SubLedgers_ReconcileWithTheControlAccounts()
    {
        var control = await Reports(s => s.GetControlAccountsAsync(CancellationToken.None));

        Assert.True(control.IsReconciled, string.Join(" | ", control.Rows.Select(x => $"{x.Control} {x.CurrencyCode}: {x.SubledgerBalance} / {x.LedgerBalance}")));
        Assert.Equal(0, control.PendingPostingEvents);
        Assert.Equal(0, control.FailedPostingEvents);

        var cashboxes = await Reports(s => s.GetCashboxBalancesAsync(CancellationToken.None));
        var damascus = Assert.Single(cashboxes.Rows, x => x.Code == "DAM-SYP-01");
        Assert.Equal(await CodTestData.CashboxBalanceAsync(_context), damascus.LedgerBalance);
        Assert.Equal(500_000m + CodTestData.Cod - CodTestData.Net - 25_000m, damascus.SubledgerBalance);
        Assert.True(cashboxes.IsReconciled);

        var drivers = await Reports(s => s.GetDriverOutstandingAsync(CancellationToken.None));
        Assert.True(drivers.IsReconciled);
        var senders = await Reports(s => s.GetSenderPayablesAsync(CancellationToken.None));
        Assert.True(senders.IsReconciled);
    }

    [Fact]
    public async Task RevenueByBranch_AddsUpToTheProfitAndLoss()
    {
        var byBranch = await Reports(s => s.GetRevenueByBranchAsync(new RevenueByBranchQuery(), CancellationToken.None));

        Assert.Equal(Revenue, byBranch.Totals.TotalRevenue);
        Assert.Equal(Expenses, byBranch.Totals.Expenses);
        Assert.Equal(Revenue - Expenses, byBranch.Totals.NetProfit);
        var damascus = Assert.Single(byBranch.Rows, x => x.BranchId == _ids.Damascus);
        Assert.Equal(30_000m, damascus.ShippingRevenue);
        Assert.Equal(3_000m, damascus.CodCommissionRevenue);
    }

    [Fact]
    public async Task ExpenseByCategory_AndCostCenterFilter()
    {
        var expenses = await Reports(s => s.GetExpenseByCategoryAsync(new ExpenseByCategoryQuery(), CancellationToken.None));

        Assert.Equal(Expenses, expenses.Total);
        Assert.Equal(475_000m, expenses.ByAccount.Single(x => x.Code == A.Fuel).Amount);
        Assert.Equal(25_000m, Assert.Single(expenses.ByExpenseCategory, x => x.ExpenseCategoryId.HasValue).Amount);
        Assert.Equal(100m, expenses.ByAccount.Sum(x => x.Percent), 1);

        var admin = await A.CostCenterIdAsync(_context, "ADMIN");
        var pl = await Reports(s => s.GetProfitLossAsync(new ProfitLossQuery { CostCenterId = admin }, CancellationToken.None));
        Assert.Equal(1_200_000m, pl.TotalExpenses);
        Assert.Equal(0m, pl.TotalRevenue);
    }

    [Fact]
    public async Task SupplierAging_ShowsTheOpenBill()
    {
        var aging = await Reports(s => s.GetSupplierAgingAsync(new PartyAgingQuery(), CancellationToken.None));

        var row = Assert.Single(aging.Rows);
        Assert.Equal(_supplierId, row.PartyId);
        Assert.Equal(450_000m, row.Days0To30);
        Assert.Equal(450_000m, row.NetBalance);
    }

    [Fact]
    public async Task BankBalance_ComesFromTheLinesOfTheBankAccount()
    {
        var banks = await Reports(s => s.GetBankBalancesAsync(CancellationToken.None));

        var row = Assert.Single(banks.Rows, x => x.Code == "BNK-CBS-SYP");
        Assert.Equal(8_800_000m, row.LedgerBalance);
        Assert.Equal(A.Banks, row.AccountCode);
    }

    [Fact]
    public async Task ReversedEntry_DropsOutOfTheStatements_WhichStillBalance()
    {
        await A.ReverseAsync(_context, _rentEntryId, "الإيجار دُفع من حساب المالك");

        var pl = await Reports(s => s.GetProfitLossAsync(new ProfitLossQuery(), CancellationToken.None));
        var trial = await Reports(s => s.GetTrialBalanceAsync(new TrialBalanceQuery(), CancellationToken.None));
        var bs = await Reports(s => s.GetBalanceSheetAsync(new BalanceSheetQuery(), CancellationToken.None));

        Assert.Equal(Expenses - 1_200_000m, pl.TotalExpenses);
        Assert.True(trial.IsBalanced);
        Assert.True(bs.IsBalanced);
        Assert.Equal(pl.NetProfit, bs.CurrentYearProfit);
    }

    [Fact]
    public async Task JournalReport_ListsThePostedEntries()
    {
        var report = await Reports(s => s.GetJournalReportAsync(new JournalReportQuery { PageSize = 100 }, CancellationToken.None));
        var postedCount = await _context.QueryAsync(db => db.JournalEntries.CountAsync(x => x.Status == Domain.Enums.JournalEntryStatus.Posted));

        Assert.Equal(postedCount, report.TotalCount);
        Assert.All(report.Items, x => Assert.True(x.IsBalanced));
    }
}
