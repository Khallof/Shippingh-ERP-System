using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Accounting;
using ShippingERP.Domain.Entities.Accounting;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Cash;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Accounting;

/// <summary>
/// Financial statements and ledger reports (step 7). Everything is computed from the posted journal lines (status
/// Posted or Reversed: a reversed entry and its reversal both stay in the ledger and cancel each other), in the
/// BASE currency, by accounting date. Sums are calculated by SQL Server (GROUP BY account); only the per-account
/// totals come back to the application.
///
/// Profit and loss accounts start every fiscal year at zero: the trial balance puts the profit of the earlier years
/// on the retained earnings account and the balance sheet shows it as "prior years' profit". No closing entry is
/// needed for the statements to balance.
///
/// The sub-ledger reports (cashboxes, drivers, senders, customers) compare each operational balance with the general
/// ledger. A difference means an operational event is still waiting for the automatic posting (the counts are in the
/// report) or a posting rule maps a role to the wrong account.
/// </summary>
public sealed class FinancialReportService : IFinancialReportService
{
    private readonly AppDbContext _dbContext;
    private readonly AccountingAccess _access;

    public FinancialReportService(AppDbContext dbContext, AccountingAccess access)
    {
        _dbContext = dbContext;
        _access = access;
    }

    private AppDbContext Db => _dbContext;

    private sealed record AccountInfo(
        long Id,
        string Code,
        string ArabicName,
        AccountType Type,
        int Level,
        bool IsPostable,
        bool IsCash,
        long? ParentId,
        long? CategoryId,
        string? CategoryCode,
        string? CategoryName,
        int CategorySort,
        CashFlowActivity Activity);

    private sealed record Totals(decimal Debit, decimal Credit)
    {
        public decimal Net => Debit - Credit;
    }

    // ------------------------------------------------------------------ building blocks

    /// <summary>Posted ledger lines, optionally of one branch and one cost center.</summary>
    private IQueryable<JournalEntryLine> Ledger(long? branchId, long? costCenterId = null, long? currencyId = null)
    {
        var lines = Db.JournalEntryLines.AsNoTracking()
            .Where(x => x.JournalEntry.Status == JournalEntryStatus.Posted || x.JournalEntry.Status == JournalEntryStatus.Reversed);

        // Step 9: one currency in its own amounts (an entry has one currency, so its lines still balance).
        if (currencyId.HasValue)
        {
            var currency = currencyId.Value;
            lines = lines.Where(x => x.JournalEntry.CurrencyId == currency);
        }

        if (branchId.HasValue)
        {
            var branch = branchId.Value;
            lines = lines.Where(x => x.BranchId == branch);
        }

        if (costCenterId.HasValue)
        {
            var costCenter = costCenterId.Value;
            lines = lines.Where(x => x.Dimensions.Any(d => d.DimensionType == DimensionType.CostCenter && d.AccountingDimensionId == costCenter));
        }

        return lines;
    }

    private static async Task<Dictionary<long, Totals>> SumByAccountAsync(IQueryable<JournalEntryLine> lines, bool original, CancellationToken ct) =>
        (await lines
            .GroupBy(x => x.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Debit = g.Sum(x => original ? x.Debit : x.DebitBase),
                Credit = g.Sum(x => original ? x.Credit : x.CreditBase)
            })
            .ToListAsync(ct))
        .ToDictionary(x => x.AccountId, x => new Totals(x.Debit, x.Credit));

    private async Task<Dictionary<long, AccountInfo>> AccountsAsync(CancellationToken ct) =>
        (await Db.Accounts.AsNoTracking().IgnoreQueryFilters()
            .Where(x => !x.IsDeleted)
            .Select(x => new AccountInfo(
                x.Id,
                x.Code,
                x.ArabicName,
                x.AccountType,
                x.Level,
                x.IsPostable,
                x.IsCashAccount,
                x.ParentAccountId,
                x.AccountCategoryId,
                x.AccountCategory != null ? x.AccountCategory.Code : null,
                x.AccountCategory != null ? x.AccountCategory.ArabicName : null,
                x.AccountCategory != null ? x.AccountCategory.SortOrder : 9999,
                x.AccountCategory != null ? x.AccountCategory.CashFlowActivity : CashFlowActivity.Operating))
            .ToListAsync(ct))
        .ToDictionary(x => x.Id);

    private async Task<(DateTime From, DateTime To)> RangeAsync(DateTime? fromDate, DateTime? toDate, CancellationToken ct)
    {
        var to = BusinessCalendar.AsDate(toDate ?? _access.Today);
        var from = fromDate.HasValue ? BusinessCalendar.AsDate(fromDate.Value) : await _access.FiscalYearStartAsync(to, ct);
        return (from, to);
    }

    private async Task<string> BaseCodeAsync(CancellationToken ct) => (await _access.BaseCurrencyAsync(ct)).Code;

    /// <summary>The currency the report is in: the requested one (own amounts) or the base currency.</summary>
    private async Task<string> CurrencyCodeAsync(long? currencyId, CancellationToken ct)
    {
        if (!currencyId.HasValue)
        {
            return await BaseCodeAsync(ct);
        }

        var id = currencyId.Value;
        return await Db.Currencies.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == id).Select(x => x.Code).FirstOrDefaultAsync(ct)
               ?? throw new NotFoundException("العملة", id);
    }

    /// <summary>The accounts mapped to a role by the active posting rules (any scope).</summary>
    private async Task<HashSet<long>> RoleAccountsAsync(string role, CancellationToken ct) =>
        (await Db.AccountingPostingRules.AsNoTracking()
            .Where(x => x.IsActive && x.AccountRole == role)
            .Select(x => x.AccountId)
            .ToListAsync(ct))
        .ToHashSet();

    private static List<long> WithDescendants(long accountId, IReadOnlyDictionary<long, AccountInfo> accounts)
    {
        var children = accounts.Values.Where(x => x.ParentId.HasValue).ToLookup(x => x.ParentId!.Value, x => x.Id);
        var result = new List<long>();
        var stack = new Stack<long>();
        stack.Push(accountId);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            result.Add(id);
            foreach (var child in children[id])
            {
                stack.Push(child);
            }
        }

        return result;
    }

    private async Task<(int Pending, int Failed)> PostingBacklogAsync(CancellationToken ct) =>
        (await Db.OutboxMessages.AsNoTracking().CountAsync(x => x.ProcessedAtUtc == null, ct),
         await Db.AccountingPostingEvents.AsNoTracking().CountAsync(x => x.Status == PostingEventStatus.Failed, ct));

    // ------------------------------------------------------------------ general ledger

    public async Task<GeneralLedgerDto> GetGeneralLedgerAsync(GeneralLedgerQuery query, CancellationToken cancellationToken)
    {
        var accounts = await AccountsAsync(cancellationToken);
        if (!accounts.TryGetValue(query.AccountId, out var account))
        {
            throw new NotFoundException("الحساب", query.AccountId);
        }

        var (from, to) = await RangeAsync(query.FromDate, query.ToDate, cancellationToken);
        var branchId = _access.ReportBranch(query.BranchId);
        var ids = WithDescendants(account.Id, accounts);
        var original = query.CurrencyId.HasValue;
        var lines = Ledger(branchId, query.CostCenterId, query.CurrencyId).Where(x => ids.Contains(x.AccountId));

        // Profit and loss accounts: the opening balance counts from the start of the fiscal year.
        var openingLines = lines.Where(x => x.JournalEntry.EntryDate < from);
        if (AccountRules.IsProfitAndLoss(account.Type))
        {
            var yearStart = await _access.FiscalYearStartAsync(from, cancellationToken);
            openingLines = openingLines.Where(x => x.JournalEntry.EntryDate >= yearStart);
        }

        var opening = (await SumByAccountAsync(openingLines, original, cancellationToken)).Values.Sum(x => x.Net);

        var period = await lines
            .Where(x => x.JournalEntry.EntryDate >= from && x.JournalEntry.EntryDate <= to)
            .Select(x => new
            {
                x.Id,
                x.JournalEntry.EntryDate,
                x.JournalEntryId,
                x.LineNumber,
                DebitBase = original ? x.Debit : x.DebitBase,
                CreditBase = original ? x.Credit : x.CreditBase
            })
            .ToListAsync(cancellationToken);
        var ordered = period.OrderBy(x => x.EntryDate).ThenBy(x => x.JournalEntryId).ThenBy(x => x.LineNumber).ToList();

        var running = new Dictionary<long, decimal>(ordered.Count);
        var balance = opening;
        foreach (var row in ordered)
        {
            balance += row.DebitBase - row.CreditBase;
            running[row.Id] = balance;
        }

        var pageIds = ordered.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).Select(x => x.Id).ToList();
        var details = await Db.JournalEntryLines.AsNoTracking()
            .Where(x => pageIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.JournalEntryId,
                x.JournalEntry.EntryNumber,
                x.JournalEntry.EntryDate,
                x.JournalEntry.JournalType,
                EntryDescription = x.JournalEntry.Description,
                LineDescription = x.Description,
                x.JournalEntry.Reference,
                x.JournalEntry.SourceNumber,
                AccountCode = x.Account.Code,
                BranchName = x.Branch.ArabicName,
                CurrencyCode = x.JournalEntry.Currency.Code,
                AmountInCurrency = x.Debit - x.Credit,
                DebitBase = original ? x.Debit : x.DebitBase,
                CreditBase = original ? x.Credit : x.CreditBase
            })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var pageLines = pageIds.Select(id => details[id]).Select(x => new GeneralLedgerLineDto
        {
            JournalEntryId = x.JournalEntryId,
            EntryNumber = x.EntryNumber,
            EntryDate = x.EntryDate,
            JournalType = x.JournalType,
            Description = x.EntryDescription,
            LineDescription = x.LineDescription,
            Reference = x.Reference,
            SourceNumber = x.SourceNumber,
            AccountCode = x.AccountCode,
            BranchName = x.BranchName,
            CurrencyCode = x.CurrencyCode,
            AmountInCurrency = x.AmountInCurrency,
            Debit = x.DebitBase,
            Credit = x.CreditBase,
            RunningBalance = running[x.Id]
        }).ToList();

        var periodDebit = ordered.Sum(x => x.DebitBase);
        var periodCredit = ordered.Sum(x => x.CreditBase);

        return new GeneralLedgerDto
        {
            AccountId = account.Id,
            AccountCode = account.Code,
            AccountName = account.ArabicName,
            AccountType = account.Type,
            NormalBalance = AccountRules.NormalBalanceOf(account.Type),
            IncludesSubAccounts = ids.Count > 1,
            FromDate = from,
            ToDate = to,
            BaseCurrencyCode = await CurrencyCodeAsync(query.CurrencyId, cancellationToken),
            OpeningBalance = opening,
            PeriodDebit = periodDebit,
            PeriodCredit = periodCredit,
            ClosingBalance = opening + periodDebit - periodCredit,
            Lines = new PagedResult<GeneralLedgerLineDto>(pageLines, query.PageNumber, query.PageSize, ordered.Count)
        };
    }

    // ------------------------------------------------------------------ trial balance

    public async Task<TrialBalanceDto> GetTrialBalanceAsync(TrialBalanceQuery query, CancellationToken cancellationToken)
    {
        var accounts = await AccountsAsync(cancellationToken);
        var (from, to) = await RangeAsync(query.FromDate, query.ToDate, cancellationToken);
        var branchId = _access.ReportBranch(query.BranchId);
        var yearStart = await _access.FiscalYearStartAsync(from, cancellationToken);
        var original = query.CurrencyId.HasValue;
        var lines = Ledger(branchId, query.CostCenterId, query.CurrencyId);

        var before = await SumByAccountAsync(lines.Where(x => x.JournalEntry.EntryDate < from), original, cancellationToken);
        var priorYears = await SumByAccountAsync(lines.Where(x => x.JournalEntry.EntryDate < yearStart), original, cancellationToken);
        var movement = await SumByAccountAsync(lines.Where(x => x.JournalEntry.EntryDate >= from && x.JournalEntry.EntryDate <= to), original, cancellationToken);

        // Opening: balance sheet accounts cumulative; P&L accounts only since the start of the fiscal year. The profit
        // of the earlier years moves to the retained earnings account so the opening columns still balance.
        var openingNet = new Dictionary<long, decimal>();
        decimal priorProfit = 0m;
        foreach (var (accountId, totals) in before)
        {
            var type = accounts.TryGetValue(accountId, out var info) ? info.Type : AccountType.Assets;
            var net = totals.Net;
            if (AccountRules.IsProfitAndLoss(type) && priorYears.TryGetValue(accountId, out var prior))
            {
                net -= prior.Net;
                priorProfit += AccountRules.ProfitEffect(prior.Debit, prior.Credit);
            }

            openingNet[accountId] = net;
        }

        var extraRows = new List<TrialBalanceRowDto>();
        if (priorProfit != 0m)
        {
            var retained = accounts.Values.FirstOrDefault(x => x.Code == _access.Options.RetainedEarningsAccountCode && x.IsPostable);
            if (retained is not null)
            {
                openingNet[retained.Id] = openingNet.GetValueOrDefault(retained.Id) - priorProfit;
            }
            else
            {
                var (debit, credit) = AccountRules.ToColumns(-priorProfit);
                extraRows.Add(new TrialBalanceRowDto
                {
                    Code = "-",
                    ArabicName = "أرباح سنوات سابقة (غير مقفلة)",
                    AccountType = AccountType.Equity,
                    Level = 1,
                    IsPostable = true,
                    OpeningDebit = debit,
                    OpeningCredit = credit,
                    ClosingDebit = debit,
                    ClosingCredit = credit
                });
            }
        }

        // Target row of each account: itself, or its ancestor at the requested level.
        long Target(long accountId)
        {
            if (!query.Level.HasValue || !accounts.TryGetValue(accountId, out var info))
            {
                return accountId;
            }

            while (info.Level > query.Level.Value && info.ParentId.HasValue && accounts.TryGetValue(info.ParentId.Value, out var parent))
            {
                info = parent;
            }

            return info.Id;
        }

        var rows = new Dictionary<long, (decimal Opening, decimal Debit, decimal Credit)>();
        void Add(long accountId, decimal opening, decimal debit, decimal credit)
        {
            var target = Target(accountId);
            var current = rows.GetValueOrDefault(target);
            rows[target] = (current.Opening + opening, current.Debit + debit, current.Credit + credit);
        }

        foreach (var (accountId, net) in openingNet)
        {
            Add(accountId, net, 0m, 0m);
        }

        foreach (var (accountId, totals) in movement)
        {
            Add(accountId, 0m, totals.Debit, totals.Credit);
        }

        if (query.IncludeZeroBalances)
        {
            var candidates = query.Level.HasValue
                ? accounts.Values.Where(x => x.Level <= query.Level.Value && (x.Level == query.Level.Value || x.IsPostable))
                : accounts.Values.Where(x => x.IsPostable);
            foreach (var account in candidates)
            {
                rows.TryAdd(account.Id, (0m, 0m, 0m));
            }
        }

        var result = rows
            .Where(x => query.IncludeZeroBalances || x.Value.Opening != 0m || x.Value.Debit != 0m || x.Value.Credit != 0m)
            .Where(x => accounts.ContainsKey(x.Key))
            .Select(x =>
            {
                var info = accounts[x.Key];
                var (openingDebit, openingCredit) = AccountRules.ToColumns(x.Value.Opening);
                var (closingDebit, closingCredit) = AccountRules.ToColumns(x.Value.Opening + x.Value.Debit - x.Value.Credit);
                return new TrialBalanceRowDto
                {
                    AccountId = info.Id,
                    Code = info.Code,
                    ArabicName = info.ArabicName,
                    AccountType = info.Type,
                    Level = info.Level,
                    IsPostable = info.IsPostable,
                    OpeningDebit = openingDebit,
                    OpeningCredit = openingCredit,
                    PeriodDebit = x.Value.Debit,
                    PeriodCredit = x.Value.Credit,
                    ClosingDebit = closingDebit,
                    ClosingCredit = closingCredit
                };
            })
            .Concat(extraRows)
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .ToList();

        var totalOpeningDebit = result.Sum(x => x.OpeningDebit);
        var totalOpeningCredit = result.Sum(x => x.OpeningCredit);
        var totalPeriodDebit = result.Sum(x => x.PeriodDebit);
        var totalPeriodCredit = result.Sum(x => x.PeriodCredit);
        var totalClosingDebit = result.Sum(x => x.ClosingDebit);
        var totalClosingCredit = result.Sum(x => x.ClosingCredit);

        return new TrialBalanceDto
        {
            FromDate = from,
            ToDate = to,
            BaseCurrencyCode = await CurrencyCodeAsync(query.CurrencyId, cancellationToken),
            Rows = result,
            TotalOpeningDebit = totalOpeningDebit,
            TotalOpeningCredit = totalOpeningCredit,
            TotalPeriodDebit = totalPeriodDebit,
            TotalPeriodCredit = totalPeriodCredit,
            TotalClosingDebit = totalClosingDebit,
            TotalClosingCredit = totalClosingCredit,
            IsBalanced = totalOpeningDebit == totalOpeningCredit && totalPeriodDebit == totalPeriodCredit && totalClosingDebit == totalClosingCredit
        };
    }

    // ------------------------------------------------------------------ profit and loss

    public async Task<ProfitLossDto> GetProfitLossAsync(ProfitLossQuery query, CancellationToken cancellationToken)
    {
        var accounts = await AccountsAsync(cancellationToken);
        var (from, to) = await RangeAsync(query.FromDate, query.ToDate, cancellationToken);
        var branchId = _access.ReportBranch(query.BranchId);
        var original = query.CurrencyId.HasValue;
        var sums = await SumByAccountAsync(
            Ledger(branchId, query.CostCenterId, query.CurrencyId).Where(x => x.JournalEntry.EntryDate >= from && x.JournalEntry.EntryDate <= to), original, cancellationToken);

        var revenue = Groups(sums, accounts, AccountType.Revenue);
        var cost = Groups(sums, accounts, AccountType.CostOfRevenue);
        var expenses = Groups(sums, accounts, AccountType.Expenses);

        var totalRevenue = revenue.Sum(x => x.Amount);
        var totalCost = cost.Sum(x => x.Amount);
        var totalExpenses = expenses.Sum(x => x.Amount);
        var gross = totalRevenue - totalCost;
        var net = gross - totalExpenses;

        return new ProfitLossDto
        {
            FromDate = from,
            ToDate = to,
            BaseCurrencyCode = await CurrencyCodeAsync(query.CurrencyId, cancellationToken),
            Revenue = revenue,
            TotalRevenue = totalRevenue,
            CostOfRevenue = cost,
            TotalCostOfRevenue = totalCost,
            GrossProfit = gross,
            Expenses = expenses,
            TotalExpenses = totalExpenses,
            NetProfit = net,
            NetProfitMarginPercent = totalRevenue == 0m ? null : Math.Round(net / totalRevenue * 100m, 2, MidpointRounding.AwayFromZero)
        };
    }

    /// <summary>The accounts of one type grouped by category, amounts in the natural sign of the type.</summary>
    private static List<StatementGroupDto> Groups(IReadOnlyDictionary<long, Totals> sums, IReadOnlyDictionary<long, AccountInfo> accounts, AccountType type) =>
        sums
            .Where(x => accounts.TryGetValue(x.Key, out var info) && info.Type == type)
            .Select(x => new { Info = accounts[x.Key], Amount = AccountRules.NaturalBalance(type, x.Value.Debit, x.Value.Credit) })
            .Where(x => x.Amount != 0m)
            .GroupBy(x => new { x.Info.CategoryId, x.Info.CategoryCode, x.Info.CategoryName, x.Info.CategorySort })
            .OrderBy(g => g.Key.CategorySort).ThenBy(g => g.Key.CategoryCode)
            .Select(g => new StatementGroupDto
            {
                CategoryId = g.Key.CategoryId,
                Code = g.Key.CategoryCode ?? "-",
                ArabicName = g.Key.CategoryName ?? "غير مصنّف",
                Amount = g.Sum(x => x.Amount),
                Accounts = g.OrderBy(x => x.Info.Code, StringComparer.Ordinal)
                    .Select(x => new StatementAccountDto { AccountId = x.Info.Id, Code = x.Info.Code, ArabicName = x.Info.ArabicName, Amount = x.Amount })
                    .ToList()
            })
            .ToList();

    // ------------------------------------------------------------------ balance sheet

    public async Task<BalanceSheetDto> GetBalanceSheetAsync(BalanceSheetQuery query, CancellationToken cancellationToken)
    {
        var accounts = await AccountsAsync(cancellationToken);
        var asOf = BusinessCalendar.AsDate(query.AsOfDate ?? _access.Today);
        var branchId = _access.ReportBranch(query.BranchId);
        var yearStart = await _access.FiscalYearStartAsync(asOf, cancellationToken);
        var original = query.CurrencyId.HasValue;
        var lines = Ledger(branchId, null, query.CurrencyId).Where(x => x.JournalEntry.EntryDate <= asOf);

        var sums = await SumByAccountAsync(lines, original, cancellationToken);
        var currentYear = await SumByAccountAsync(lines.Where(x => x.JournalEntry.EntryDate >= yearStart), original, cancellationToken);

        decimal currentProfit = 0m, totalProfit = 0m;
        foreach (var (accountId, totals) in sums)
        {
            if (accounts.TryGetValue(accountId, out var info) && AccountRules.IsProfitAndLoss(info.Type))
            {
                totalProfit += AccountRules.ProfitEffect(totals.Debit, totals.Credit);
                if (currentYear.TryGetValue(accountId, out var year))
                {
                    currentProfit += AccountRules.ProfitEffect(year.Debit, year.Credit);
                }
            }
        }

        var assets = Groups(sums, accounts, AccountType.Assets);
        var liabilities = Groups(sums, accounts, AccountType.Liabilities);
        var equity = Groups(sums, accounts, AccountType.Equity);

        var totalAssets = assets.Sum(x => x.Amount);
        var totalLiabilities = liabilities.Sum(x => x.Amount);
        var priorProfit = totalProfit - currentProfit;
        var totalEquity = equity.Sum(x => x.Amount) + priorProfit + currentProfit;
        var liabilitiesAndEquity = totalLiabilities + totalEquity;
        var interBranch = branchId.HasValue ? totalAssets - liabilitiesAndEquity : 0m;

        return new BalanceSheetDto
        {
            AsOfDate = asOf,
            BaseCurrencyCode = await CurrencyCodeAsync(query.CurrencyId, cancellationToken),
            Assets = assets,
            TotalAssets = totalAssets,
            Liabilities = liabilities,
            TotalLiabilities = totalLiabilities,
            Equity = equity,
            PriorYearsProfit = priorProfit,
            CurrentYearProfit = currentProfit,
            TotalEquity = totalEquity,
            TotalLiabilitiesAndEquity = liabilitiesAndEquity,
            InterBranchDifference = interBranch,
            IsBalanced = totalAssets == liabilitiesAndEquity + interBranch
        };
    }

    // ------------------------------------------------------------------ cash flow (direct method)

    public async Task<CashFlowDto> GetCashFlowAsync(CashFlowQuery query, CancellationToken cancellationToken)
    {
        var accounts = await AccountsAsync(cancellationToken);
        var (from, to) = await RangeAsync(query.FromDate, query.ToDate, cancellationToken);
        var branchId = _access.ReportBranch(query.BranchId);
        var cashIds = accounts.Values.Where(x => x.IsCash && x.IsPostable).Select(x => x.Id).ToList();

        var original = query.CurrencyId.HasValue;
        var ledger = Ledger(branchId, null, query.CurrencyId);
        var cashLines = ledger.Where(x => cashIds.Contains(x.AccountId));
        var periodLines = ledger.Where(x => x.JournalEntry.EntryDate >= from && x.JournalEntry.EntryDate <= to);

        // Each non-cash line of an entry that moves cash is a source (credit) or a use (debit) of that cash: the sum of
        // these lines equals the net cash change of the entry because the entry is balanced. Transfers between cash
        // accounts have no non-cash line and do not appear.
        var counterparts = await SumByAccountAsync(
            periodLines.Where(x => !cashIds.Contains(x.AccountId) &&
                                   periodLines.Any(c => c.JournalEntryId == x.JournalEntryId && cashIds.Contains(c.AccountId))),
            original,
            cancellationToken);

        var opening = await SumByAccountAsync(cashLines.Where(x => x.JournalEntry.EntryDate < from), original, cancellationToken);
        var closing = await SumByAccountAsync(cashLines.Where(x => x.JournalEntry.EntryDate <= to), original, cancellationToken);

        var sections = counterparts
            .Where(x => accounts.ContainsKey(x.Key))
            .Select(x => new { Info = accounts[x.Key], Inflow = x.Value.Credit, Outflow = x.Value.Debit })
            .GroupBy(x => x.Info.Activity == CashFlowActivity.None ? CashFlowActivity.Operating : x.Info.Activity)
            .OrderBy(g => g.Key)
            .Select(g => new CashFlowSectionDto
            {
                Activity = g.Key,
                ActivityName = AccountingNames.Arabic(g.Key),
                Lines = g.OrderBy(x => x.Info.Code, StringComparer.Ordinal)
                    .Select(x => new CashFlowLineDto
                    {
                        AccountId = x.Info.Id,
                        Code = x.Info.Code,
                        ArabicName = x.Info.ArabicName,
                        Inflow = x.Inflow,
                        Outflow = x.Outflow,
                        Net = x.Inflow - x.Outflow
                    })
                    .ToList(),
                TotalInflow = g.Sum(x => x.Inflow),
                TotalOutflow = g.Sum(x => x.Outflow),
                Net = g.Sum(x => x.Inflow - x.Outflow)
            })
            .ToList();

        var openingCash = opening.Values.Sum(x => x.Net);
        var closingCash = closing.Values.Sum(x => x.Net);
        var netChange = sections.Sum(x => x.Net);

        var cashAccounts = cashIds
            .Where(id => opening.ContainsKey(id) || closing.ContainsKey(id))
            .Select(id => new CashAccountBalanceDto
            {
                AccountId = id,
                Code = accounts[id].Code,
                ArabicName = accounts[id].ArabicName,
                Opening = opening.TryGetValue(id, out var o) ? o.Net : 0m,
                Closing = closing.TryGetValue(id, out var c) ? c.Net : 0m
            })
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .ToList();

        return new CashFlowDto
        {
            FromDate = from,
            ToDate = to,
            BaseCurrencyCode = await CurrencyCodeAsync(query.CurrencyId, cancellationToken),
            OpeningCash = openingCash,
            Sections = sections,
            NetChange = netChange,
            ClosingCash = closingCash,
            CashAccounts = cashAccounts,
            IsReconciled = openingCash + netChange == closingCash
        };
    }

    // ------------------------------------------------------------------ journal report

    public async Task<PagedResult<JournalEntryDto>> GetJournalReportAsync(JournalReportQuery query, CancellationToken cancellationToken)
    {
        var (from, to) = await RangeAsync(query.FromDate, query.ToDate, cancellationToken);
        var branchId = _access.ReportBranch(query.BranchId);

        var rows = Db.JournalEntries.AsNoTracking()
            .Where(x => (x.Status == JournalEntryStatus.Posted || x.Status == JournalEntryStatus.Reversed) && x.EntryDate >= from && x.EntryDate <= to);

        if (query.JournalType.HasValue)
        {
            var type = query.JournalType.Value;
            rows = rows.Where(x => x.JournalType == type);
        }

        if (branchId.HasValue)
        {
            var branch = branchId.Value;
            rows = rows.Where(x => x.BranchId == branch || x.Lines.Any(l => l.BranchId == branch));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.EntryNumber.Contains(search) || x.Description.Contains(search) || (x.SourceNumber != null && x.SourceNumber.Contains(search)));
        }

        var total = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderBy(x => x.EntryDate).ThenBy(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(AccountingProjections.Journal)
            .ToListAsync(cancellationToken);

        return new PagedResult<JournalEntryDto>(items.Select(x => x.WithNames()).ToList(), query.PageNumber, query.PageSize, total);
    }

    // ------------------------------------------------------------------ revenue by branch, expenses by category

    public async Task<RevenueByBranchDto> GetRevenueByBranchAsync(RevenueByBranchQuery query, CancellationToken cancellationToken)
    {
        var accounts = await AccountsAsync(cancellationToken);
        var (from, to) = await RangeAsync(query.FromDate, query.ToDate, cancellationToken);
        var branchId = _access.ForcedBranchId;

        var shipping = await RoleAccountsAsync(AccountRoles.ShippingRevenue, cancellationToken);
        var commission = await RoleAccountsAsync(AccountRoles.CodCommissionRevenue, cancellationToken);
        var services = await RoleAccountsAsync(AccountRoles.AdditionalServiceRevenue, cancellationToken);

        var original = query.CurrencyId.HasValue;
        var sums = await Ledger(branchId, null, query.CurrencyId)
            .Where(x => x.JournalEntry.EntryDate >= from && x.JournalEntry.EntryDate <= to)
            .GroupBy(x => new { x.BranchId, x.AccountId })
            .Select(g => new
            {
                g.Key.BranchId,
                g.Key.AccountId,
                Debit = g.Sum(x => original ? x.Debit : x.DebitBase),
                Credit = g.Sum(x => original ? x.Credit : x.CreditBase)
            })
            .ToListAsync(cancellationToken);

        var branchIds = sums.Select(x => x.BranchId).Distinct().ToList();
        var names = await Db.Branches.AsNoTracking().IgnoreQueryFilters()
            .Where(x => branchIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.ArabicName, cancellationToken);

        var rows = new List<RevenueByBranchRowDto>();
        foreach (var branch in sums.GroupBy(x => x.BranchId))
        {
            decimal ship = 0m, comm = 0m, serv = 0m, other = 0m, cost = 0m, expense = 0m;
            foreach (var item in branch)
            {
                if (!accounts.TryGetValue(item.AccountId, out var info))
                {
                    continue;
                }

                switch (info.Type)
                {
                    case AccountType.Revenue:
                        var amount = item.Credit - item.Debit;
                        if (shipping.Contains(info.Id))
                        {
                            ship += amount;
                        }
                        else if (commission.Contains(info.Id))
                        {
                            comm += amount;
                        }
                        else if (services.Contains(info.Id))
                        {
                            serv += amount;
                        }
                        else
                        {
                            other += amount;
                        }

                        break;
                    case AccountType.CostOfRevenue:
                        cost += item.Debit - item.Credit;
                        break;
                    case AccountType.Expenses:
                        expense += item.Debit - item.Credit;
                        break;
                }
            }

            if (ship == 0m && comm == 0m && serv == 0m && other == 0m && cost == 0m && expense == 0m)
            {
                continue;
            }

            var totalRevenue = ship + comm + serv + other;
            rows.Add(new RevenueByBranchRowDto
            {
                BranchId = branch.Key,
                BranchName = names.GetValueOrDefault(branch.Key, branch.Key.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ShippingRevenue = ship,
                CodCommissionRevenue = comm,
                AdditionalServiceRevenue = serv,
                OtherRevenue = other,
                TotalRevenue = totalRevenue,
                CostOfRevenue = cost,
                Expenses = expense,
                NetProfit = totalRevenue - cost - expense
            });
        }

        rows = rows.OrderByDescending(x => x.TotalRevenue).ThenBy(x => x.BranchName).ToList();

        return new RevenueByBranchDto
        {
            FromDate = from,
            ToDate = to,
            BaseCurrencyCode = await CurrencyCodeAsync(query.CurrencyId, cancellationToken),
            Rows = rows,
            Totals = new RevenueByBranchRowDto
            {
                BranchName = "الإجمالي",
                ShippingRevenue = rows.Sum(x => x.ShippingRevenue),
                CodCommissionRevenue = rows.Sum(x => x.CodCommissionRevenue),
                AdditionalServiceRevenue = rows.Sum(x => x.AdditionalServiceRevenue),
                OtherRevenue = rows.Sum(x => x.OtherRevenue),
                TotalRevenue = rows.Sum(x => x.TotalRevenue),
                CostOfRevenue = rows.Sum(x => x.CostOfRevenue),
                Expenses = rows.Sum(x => x.Expenses),
                NetProfit = rows.Sum(x => x.NetProfit)
            }
        };
    }

    public async Task<ExpenseByCategoryDto> GetExpenseByCategoryAsync(ExpenseByCategoryQuery query, CancellationToken cancellationToken)
    {
        var accounts = await AccountsAsync(cancellationToken);
        var (from, to) = await RangeAsync(query.FromDate, query.ToDate, cancellationToken);
        var branchId = _access.ReportBranch(query.BranchId);
        var costIds = accounts.Values.Where(x => x.Type is AccountType.Expenses or AccountType.CostOfRevenue).Select(x => x.Id).ToList();

        var original = query.CurrencyId.HasValue;
        var lines = Ledger(branchId, query.CostCenterId, query.CurrencyId)
            .Where(x => x.JournalEntry.EntryDate >= from && x.JournalEntry.EntryDate <= to && costIds.Contains(x.AccountId));

        var byAccount = await SumByAccountAsync(lines, original, cancellationToken);
        var byCategory = await lines
            .GroupBy(x => x.ExpenseCategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                Debit = g.Sum(x => original ? x.Debit : x.DebitBase),
                Credit = g.Sum(x => original ? x.Credit : x.CreditBase)
            })
            .ToListAsync(cancellationToken);

        var categoryIds = byCategory.Where(x => x.CategoryId.HasValue).Select(x => x.CategoryId!.Value).ToList();
        var categoryNames = await Db.ExpenseCategories.AsNoTracking().IgnoreQueryFilters()
            .Where(x => categoryIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.ArabicName, cancellationToken);

        var total = byAccount.Values.Sum(x => x.Net);
        decimal Percent(decimal amount) => total == 0m ? 0m : Math.Round(amount / total * 100m, 2, MidpointRounding.AwayFromZero);

        return new ExpenseByCategoryDto
        {
            FromDate = from,
            ToDate = to,
            BaseCurrencyCode = await CurrencyCodeAsync(query.CurrencyId, cancellationToken),
            ByAccount = byAccount
                .Where(x => x.Value.Net != 0m)
                .Select(x => new ExpenseAccountRowDto
                {
                    AccountId = x.Key,
                    Code = accounts[x.Key].Code,
                    ArabicName = accounts[x.Key].ArabicName,
                    CategoryName = accounts[x.Key].CategoryName ?? "غير مصنّف",
                    AccountType = accounts[x.Key].Type,
                    Amount = x.Value.Net,
                    Percent = Percent(x.Value.Net)
                })
                .OrderByDescending(x => x.Amount)
                .ToList(),
            ByExpenseCategory = byCategory
                .Where(x => x.Debit - x.Credit != 0m)
                .Select(x => new ExpenseCategoryRowDto
                {
                    ExpenseCategoryId = x.CategoryId,
                    ArabicName = x.CategoryId.HasValue ? categoryNames.GetValueOrDefault(x.CategoryId.Value, "-") : "بدون فئة مصروف",
                    Amount = x.Debit - x.Credit,
                    Percent = Percent(x.Debit - x.Credit)
                })
                .OrderByDescending(x => x.Amount)
                .ToList(),
            Total = total
        };
    }

    // ------------------------------------------------------------------ aging (FIFO on the ledger)

    public Task<PartyAgingDto> GetSupplierAgingAsync(PartyAgingQuery query, CancellationToken cancellationToken) =>
        AgingAsync(query, AccountRoles.AccountsPayable, isSupplier: true, cancellationToken);

    public Task<PartyAgingDto> GetCustomerAgingAsync(PartyAgingQuery query, CancellationToken cancellationToken) =>
        AgingAsync(query, AccountRoles.AccountsReceivable, isSupplier: false, cancellationToken);

    private async Task<PartyAgingDto> AgingAsync(PartyAgingQuery query, string role, bool isSupplier, CancellationToken cancellationToken)
    {
        var asOf = BusinessCalendar.AsDate(query.AsOfDate ?? _access.Today);
        var branchId = _access.ReportBranch(query.BranchId);
        var accountIds = await RoleAccountsAsync(role, cancellationToken);

        var original = query.CurrencyId.HasValue;
        var lines = Ledger(branchId, null, query.CurrencyId).Where(x => x.JournalEntry.EntryDate <= asOf && (accountIds.Contains(x.AccountId) || x.AccountRole == role));
        lines = isSupplier ? lines.Where(x => x.SupplierId != null) : lines.Where(x => x.CustomerId != null);

        var movements = await lines
            .Select(x => new
            {
                PartyId = isSupplier ? x.SupplierId!.Value : x.CustomerId!.Value,
                x.JournalEntry.EntryDate,
                x.Id,
                DebitBase = original ? x.Debit : x.DebitBase,
                CreditBase = original ? x.Credit : x.CreditBase
            })
            .ToListAsync(cancellationToken);

        var partyIds = movements.Select(x => x.PartyId).Distinct().ToList();
        var parties = isSupplier
            ? await Db.Suppliers.AsNoTracking().IgnoreQueryFilters().Where(x => partyIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => (x.Code, x.ArabicName), cancellationToken)
            : await Db.Customers.AsNoTracking().IgnoreQueryFilters().Where(x => partyIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => (x.Code, x.ArabicName), cancellationToken);

        var rows = new List<PartyAgingRowDto>();
        foreach (var party in movements.GroupBy(x => x.PartyId))
        {
            // Supplier: a credit is a bill (increase), a debit a payment. Customer: the opposite.
            var aging = OpenItemAging.Compute(
                party.OrderBy(x => x.EntryDate).ThenBy(x => x.Id)
                    .Select(x => isSupplier
                        ? new PartyMovement(x.EntryDate, x.CreditBase, x.DebitBase)
                        : new PartyMovement(x.EntryDate, x.DebitBase, x.CreditBase)),
                asOf);

            if (aging.TotalOpen == 0m && aging.Unapplied == 0m)
            {
                continue;
            }

            var (code, name) = parties.TryGetValue(party.Key, out var p) ? p : ("-", "-");
            rows.Add(new PartyAgingRowDto
            {
                PartyId = party.Key,
                PartyCode = code,
                PartyName = name,
                Days0To30 = aging.Days0To30,
                Days31To60 = aging.Days31To60,
                Days61To90 = aging.Days61To90,
                Over90Days = aging.Over90Days,
                TotalOpen = aging.TotalOpen,
                Unapplied = aging.Unapplied,
                NetBalance = aging.NetBalance
            });
        }

        rows = rows.OrderByDescending(x => x.NetBalance).ThenBy(x => x.PartyCode).ToList();

        return new PartyAgingDto
        {
            AsOfDate = asOf,
            PartyType = isSupplier ? "Supplier" : "Customer",
            BaseCurrencyCode = await CurrencyCodeAsync(query.CurrencyId, cancellationToken),
            Rows = rows,
            Totals = new PartyAgingRowDto
            {
                PartyCode = "-",
                PartyName = "الإجمالي",
                Days0To30 = rows.Sum(x => x.Days0To30),
                Days31To60 = rows.Sum(x => x.Days31To60),
                Days61To90 = rows.Sum(x => x.Days61To90),
                Over90Days = rows.Sum(x => x.Over90Days),
                TotalOpen = rows.Sum(x => x.TotalOpen),
                Unapplied = rows.Sum(x => x.Unapplied),
                NetBalance = rows.Sum(x => x.NetBalance)
            }
        };
    }

    // ------------------------------------------------------------------ sub-ledgers against the general ledger

    public async Task<SubledgerReconciliationDto> GetCashboxBalancesAsync(CancellationToken cancellationToken)
    {
        var branchId = _access.ForcedBranchId;
        var cashboxes = Db.Cashboxes.AsNoTracking();
        if (branchId.HasValue)
        {
            var branch = branchId.Value;
            cashboxes = cashboxes.Where(x => x.BranchId == branch);
        }

        var boxes = await cashboxes
            .OrderBy(x => x.Code)
            .Select(x => new { x.Id, x.Code, x.ArabicName, BranchName = x.Branch.ArabicName, CurrencyCode = x.Currency.Code, x.CurrentBalance })
            .ToListAsync(cancellationToken);

        var ledger = (await Ledger(null)
                .Where(x => x.AccountRole == AccountRoles.CashOnHand && x.CashboxId != null)
                .GroupBy(x => x.CashboxId!.Value)
                .Select(g => new { CashboxId = g.Key, Net = g.Sum(x => x.Debit) - g.Sum(x => x.Credit) })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.CashboxId, x => x.Net);

        var rows = boxes.Select(x =>
        {
            var book = ledger.GetValueOrDefault(x.Id);
            return new SubledgerBalanceRowDto
            {
                ReferenceId = x.Id,
                Code = x.Code,
                Name = x.ArabicName,
                BranchName = x.BranchName,
                CurrencyCode = x.CurrencyCode,
                SubledgerBalance = x.CurrentBalance,
                LedgerBalance = book,
                Difference = x.CurrentBalance - book
            };
        }).ToList();

        return await SubledgerAsync("أرصدة الصناديق مقابل الأستاذ العام", rows, cancellationToken);
    }

    public async Task<SubledgerReconciliationDto> GetDriverOutstandingAsync(CancellationToken cancellationToken)
    {
        var branchId = _access.ForcedBranchId;
        var accounts = Db.DriverCashAccounts.AsNoTracking();
        if (branchId.HasValue)
        {
            var branch = branchId.Value;
            accounts = accounts.Where(x => x.Driver.Employee.BranchId == branch);
        }

        var subledger = await accounts
            .Select(x => new
            {
                x.DriverId,
                x.CurrencyId,
                Code = x.Driver.Employee.Code,
                Name = x.Driver.Employee.ArabicName,
                BranchName = x.Driver.Employee.Branch.ArabicName,
                CurrencyCode = x.Currency.Code,
                x.Balance
            })
            .ToListAsync(cancellationToken);

        var ledger = await Ledger(null)
            .Where(x => x.AccountRole == AccountRoles.DriverCustody && x.DriverId != null)
            .GroupBy(x => new { DriverId = x.DriverId!.Value, x.JournalEntry.CurrencyId })
            .Select(g => new { g.Key.DriverId, g.Key.CurrencyId, Net = g.Sum(x => x.Debit) - g.Sum(x => x.Credit) })
            .ToListAsync(cancellationToken);

        var withDriver = await Db.CODTransactions.AsNoTracking()
            .Where(x => x.Status == CODStatus.CollectedByDriver && x.DriverId != null)
            .GroupBy(x => new { DriverId = x.DriverId!.Value, x.CurrencyId })
            .Select(g => new { g.Key.DriverId, g.Key.CurrencyId, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var ledgerByKey = ledger.ToDictionary(x => (x.DriverId, x.CurrencyId), x => x.Net);
        var countByKey = withDriver.ToDictionary(x => (x.DriverId, x.CurrencyId), x => x.Count);

        var rows = subledger.Select(x =>
        {
            var book = ledgerByKey.GetValueOrDefault((x.DriverId, x.CurrencyId));
            ledgerByKey.Remove((x.DriverId, x.CurrencyId));
            return new SubledgerBalanceRowDto
            {
                ReferenceId = x.DriverId,
                Code = x.Code,
                Name = x.Name,
                BranchName = x.BranchName,
                CurrencyCode = x.CurrencyCode,
                SubledgerBalance = x.Balance,
                LedgerBalance = book,
                Difference = x.Balance - book,
                Extra = countByKey.GetValueOrDefault((x.DriverId, x.CurrencyId))
            };
        }).ToList();

        rows.AddRange(await OrphansAsync(ledgerByKey, isDriver: true, cancellationToken));
        return await SubledgerAsync("عهد السائقين (مبالغ التحصيل) مقابل الأستاذ العام", rows.Where(x => x.SubledgerBalance != 0m || x.LedgerBalance != 0m).ToList(), cancellationToken);
    }

    public async Task<SubledgerReconciliationDto> GetSenderPayablesAsync(CancellationToken cancellationToken)
    {
        var branchId = _access.ForcedBranchId;
        var cods = Db.CODTransactions.AsNoTracking();
        if (branchId.HasValue)
        {
            var branch = branchId.Value;
            cods = cods.Where(x => x.OriginBranchId == branch);
        }

        var collected = await cods
            .Where(x => x.CollectedAmount != 0m)
            .GroupBy(x => new { x.SenderCustomerId, x.CurrencyId })
            .Select(g => new
            {
                g.Key.SenderCustomerId,
                g.Key.CurrencyId,
                Collected = g.Sum(x => x.CollectedAmount),
                Unsettled = g.Where(x => x.Status == CODStatus.ReadyForSenderSettlement || x.Status == CODStatus.PartiallySettled)
                    .Sum(x => x.NetPayableAmount - x.SettledAmount)
            })
            .ToListAsync(cancellationToken);

        var released = await Db.SenderSettlementLines.AsNoTracking()
            .Where(x => x.SenderSettlement.Status == SettlementStatus.Paid && (!branchId.HasValue || x.CODTransaction.OriginBranchId == branchId.Value))
            .GroupBy(x => new { x.SenderSettlement.SenderCustomerId, x.SenderSettlement.CurrencyId })
            .Select(g => new { g.Key.SenderCustomerId, g.Key.CurrencyId, Gross = g.Sum(x => x.GrossReleased) })
            .ToListAsync(cancellationToken);

        var ledger = await Ledger(branchId)
            .Where(x => x.AccountRole == AccountRoles.CodPayableToSenders && x.CustomerId != null)
            .GroupBy(x => new { CustomerId = x.CustomerId!.Value, x.JournalEntry.CurrencyId })
            .Select(g => new { g.Key.CustomerId, g.Key.CurrencyId, Net = g.Sum(x => x.Credit) - g.Sum(x => x.Debit) })
            .ToListAsync(cancellationToken);

        var keys = collected.Select(x => (x.SenderCustomerId, x.CurrencyId))
            .Concat(released.Select(x => (x.SenderCustomerId, x.CurrencyId)))
            .Concat(ledger.Select(x => (SenderCustomerId: x.CustomerId, x.CurrencyId)))
            .Distinct()
            .ToList();

        var customerIds = keys.Select(x => x.SenderCustomerId).Distinct().ToList();
        var currencyIds = keys.Select(x => x.CurrencyId).Distinct().ToList();
        var customers = await Db.Customers.AsNoTracking().IgnoreQueryFilters().Where(x => customerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => (x.Code, x.ArabicName), cancellationToken);
        var currencies = await Db.Currencies.AsNoTracking().IgnoreQueryFilters().Where(x => currencyIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);

        var rows = keys.Select(key =>
            {
                var c = collected.FirstOrDefault(x => x.SenderCustomerId == key.SenderCustomerId && x.CurrencyId == key.CurrencyId);
                var r = released.FirstOrDefault(x => x.SenderCustomerId == key.SenderCustomerId && x.CurrencyId == key.CurrencyId);
                var l = ledger.FirstOrDefault(x => x.CustomerId == key.SenderCustomerId && x.CurrencyId == key.CurrencyId);
                var sub = (c?.Collected ?? 0m) - (r?.Gross ?? 0m);
                var book = l?.Net ?? 0m;
                var (code, name) = customers.TryGetValue(key.SenderCustomerId, out var customer) ? customer : ("-", "-");
                return new SubledgerBalanceRowDto
                {
                    ReferenceId = key.SenderCustomerId,
                    Code = code,
                    Name = name,
                    CurrencyCode = currencies.GetValueOrDefault(key.CurrencyId, "-"),
                    SubledgerBalance = sub,
                    LedgerBalance = book,
                    Difference = sub - book,
                    Extra = c?.Unsettled ?? 0m
                };
            })
            .Where(x => x.SubledgerBalance != 0m || x.LedgerBalance != 0m)
            .OrderByDescending(x => x.SubledgerBalance)
            .ToList();

        return await SubledgerAsync("أمانات المرسلين (الدفع عند الاستلام) مقابل الأستاذ العام", rows, cancellationToken);
    }

    public async Task<BankBalancesDto> GetBankBalancesAsync(CancellationToken cancellationToken)
    {
        var today = _access.Today;
        var banks = await Db.BankAccounts.AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new { x.Id, x.Code, x.ArabicName, x.BankName, x.CurrencyId, CurrencyCode = x.Currency.Code, x.AccountId })
            .ToListAsync(cancellationToken);

        var bankRuleAccounts = await RoleAccountsAsync(AccountRoles.Bank, cancellationToken);
        var accountCodes = await Db.Accounts.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);
        var rows = new List<BankBalanceRowDto>();

        foreach (var bank in banks)
        {
            var lines = Ledger(null);
            lines = bank.AccountId.HasValue
                ? lines.Where(x => x.AccountId == bank.AccountId.Value)
                : lines.Where(x => x.BankAccountId == bank.Id && bankRuleAccounts.Contains(x.AccountId));

            var totals = await lines
                .GroupBy(x => x.JournalEntry.CurrencyId == bank.CurrencyId)
                .Select(g => new { Same = g.Key, Net = g.Sum(x => x.Debit) - g.Sum(x => x.Credit), NetBase = g.Sum(x => x.DebitBase) - g.Sum(x => x.CreditBase) })
                .ToListAsync(cancellationToken);

            var last = await Db.BankReconciliations.AsNoTracking()
                .Where(x => x.BankAccountId == bank.Id && x.Status == BankReconciliationStatus.Completed)
                .OrderByDescending(x => x.StatementDate)
                .Select(x => new { x.StatementDate, x.StatementEndingBalance })
                .FirstOrDefaultAsync(cancellationToken);

            var unreconciled = await Db.BankTransactions.AsNoTracking().CountAsync(x => x.BankAccountId == bank.Id && !x.IsReconciled, cancellationToken);
            var accountId = bank.AccountId ?? bankRuleAccounts.FirstOrDefault();

            rows.Add(new BankBalanceRowDto
            {
                BankAccountId = bank.Id,
                Code = bank.Code,
                Name = bank.ArabicName,
                BankName = bank.BankName,
                CurrencyCode = bank.CurrencyCode,
                AccountCode = accountCodes.GetValueOrDefault(accountId, "-"),
                LedgerBalance = totals.Sum(x => x.Same ? x.Net : x.NetBase),
                LedgerBalanceBase = totals.Sum(x => x.NetBase),
                LastReconciledDate = last?.StatementDate,
                LastStatementBalance = last?.StatementEndingBalance,
                UnreconciledStatementLines = unreconciled
            });
        }

        return new BankBalancesDto
        {
            AsOfDate = today,
            BaseCurrencyCode = await BaseCodeAsync(cancellationToken),
            Rows = rows,
            TotalBase = rows.Sum(x => x.LedgerBalanceBase),
            TotalsByCurrency = rows
                .GroupBy(x => x.CurrencyCode)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new BankCurrencyTotalDto { CurrencyCode = g.Key, LedgerBalance = g.Sum(x => x.LedgerBalance) })
                .ToList()
        };
    }

    public async Task<ControlAccountsDto> GetControlAccountsAsync(CancellationToken cancellationToken)
    {
        var rows = new List<ControlAccountRowDto>();

        void AddTotals(string control, string name, SubledgerReconciliationDto report) =>
            rows.AddRange(report.Totals.Select(x => new ControlAccountRowDto
            {
                Control = control,
                ArabicName = name,
                CurrencyCode = x.CurrencyCode,
                SubledgerBalance = x.SubledgerBalance,
                LedgerBalance = x.LedgerBalance,
                Difference = x.Difference
            }));

        AddTotals(AccountRoles.CashOnHand, "الصناديق", await GetCashboxBalancesAsync(cancellationToken));
        AddTotals(AccountRoles.DriverCustody, "عهد السائقين", await GetDriverOutstandingAsync(cancellationToken));
        AddTotals(AccountRoles.CodPayableToSenders, "أمانات المرسلين", await GetSenderPayablesAsync(cancellationToken));
        rows.AddRange(await ReceivablesControlAsync(cancellationToken));

        var (pending, failed) = await PostingBacklogAsync(cancellationToken);
        return new ControlAccountsDto
        {
            GeneratedAtUtc = _access.UtcNow,
            Rows = rows,
            PendingPostingEvents = pending,
            FailedPostingEvents = failed,
            IsReconciled = rows.All(x => x.Difference == 0m)
        };
    }

    /// <summary>Customer receivables: the customer account transactions (step 5) against the receivable lines.</summary>
    private async Task<List<ControlAccountRowDto>> ReceivablesControlAsync(CancellationToken cancellationToken)
    {
        var branchId = _access.ForcedBranchId;
        var transactions = Db.CustomerAccountTransactions.AsNoTracking();
        if (branchId.HasValue)
        {
            var branch = branchId.Value;
            transactions = transactions.Where(x => x.BranchId == branch);
        }

        var subledger = await transactions
            .GroupBy(x => x.CurrencyId)
            .Select(g => new { CurrencyId = g.Key, Net = g.Sum(x => x.Debit) - g.Sum(x => x.Credit) })
            .ToListAsync(cancellationToken);

        var receivableAccounts = await RoleAccountsAsync(AccountRoles.AccountsReceivable, cancellationToken);
        var ledger = await Ledger(branchId)
            .Where(x => x.CustomerId != null && (x.AccountRole == AccountRoles.AccountsReceivable || receivableAccounts.Contains(x.AccountId)))
            .GroupBy(x => x.JournalEntry.CurrencyId)
            .Select(g => new { CurrencyId = g.Key, Net = g.Sum(x => x.Debit) - g.Sum(x => x.Credit) })
            .ToListAsync(cancellationToken);

        var currencyIds = subledger.Select(x => x.CurrencyId).Concat(ledger.Select(x => x.CurrencyId)).Distinct().ToList();
        var codes = await Db.Currencies.AsNoTracking().IgnoreQueryFilters().Where(x => currencyIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);

        return currencyIds.Select(id =>
        {
            var sub = subledger.FirstOrDefault(x => x.CurrencyId == id)?.Net ?? 0m;
            var book = ledger.FirstOrDefault(x => x.CurrencyId == id)?.Net ?? 0m;
            return new ControlAccountRowDto
            {
                Control = AccountRoles.AccountsReceivable,
                ArabicName = "ذمم العملاء",
                CurrencyCode = codes.GetValueOrDefault(id, "-"),
                SubledgerBalance = sub,
                LedgerBalance = book,
                Difference = sub - book
            };
        }).ToList();
    }

    /// <summary>Ledger balances of drivers without a driver cash account (should not exist; shown so nothing hides).</summary>
    private async Task<List<SubledgerBalanceRowDto>> OrphansAsync(Dictionary<(long Id, long CurrencyId), decimal> remaining, bool isDriver, CancellationToken ct)
    {
        var result = new List<SubledgerBalanceRowDto>();
        if (remaining.Count == 0 || !isDriver)
        {
            return result;
        }

        var currencyIds = remaining.Keys.Select(x => x.CurrencyId).Distinct().ToList();
        var codes = await Db.Currencies.AsNoTracking().IgnoreQueryFilters().Where(x => currencyIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Code, ct);

        foreach (var ((id, currencyId), net) in remaining.Where(x => x.Value != 0m))
        {
            result.Add(new SubledgerBalanceRowDto
            {
                ReferenceId = id,
                Code = "-",
                Name = "سائق بلا حساب عهدة",
                CurrencyCode = codes.GetValueOrDefault(currencyId, "-"),
                SubledgerBalance = 0m,
                LedgerBalance = net,
                Difference = -net
            });
        }

        return result;
    }

    private async Task<SubledgerReconciliationDto> SubledgerAsync(string title, List<SubledgerBalanceRowDto> rows, CancellationToken cancellationToken)
    {
        var (pending, failed) = await PostingBacklogAsync(cancellationToken);
        return new SubledgerReconciliationDto
        {
            Title = title,
            GeneratedAtUtc = _access.UtcNow,
            Rows = rows,
            Totals = rows
                .GroupBy(x => x.CurrencyCode)
                .OrderBy(g => g.Key)
                .Select(g => new CurrencyTotalDto
                {
                    CurrencyCode = g.Key,
                    SubledgerBalance = g.Sum(x => x.SubledgerBalance),
                    LedgerBalance = g.Sum(x => x.LedgerBalance),
                    Difference = g.Sum(x => x.Difference)
                })
                .ToList(),
            PendingPostingEvents = pending,
            FailedPostingEvents = failed,
            IsReconciled = rows.All(x => x.Difference == 0m)
        };
    }
}
