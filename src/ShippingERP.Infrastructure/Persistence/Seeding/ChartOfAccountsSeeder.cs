using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Accounting;
using ShippingERP.Domain.Entities.Accounting;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Accounting;
using ShippingERP.Infrastructure.Cash;
using R = ShippingERP.Application.IntegrationEvents.AccountRoles;

namespace ShippingERP.Infrastructure.Persistence.Seeding;

/// <summary>
/// The accounting set-up every installation needs (step 7). It ALWAYS runs at start-up and only adds what is missing:
///
///   1. The SQL Server triggers that protect posted entries (AccountingDatabaseObjects, CREATE OR ALTER).
///   2. When the chart is empty: the account categories, the default chart of accounts of a courier company
///      (assets, liabilities, equity, revenue, cost of revenue, expenses), the cost centers and the automatic
///      posting rules (one per account role, plus one per demo expense category: FUEL, MAINT, SALARY...).
///   3. A general posting rule for every role that has none (a role added by a later version).
///   4. A fiscal year (January to December, 12 monthly periods) for every calendar year from the oldest operational
///      event still waiting for the automatic posting up to the current year, so the existing events of steps 5
///      and 6 can be posted.
/// </summary>
public sealed class ChartOfAccountsSeeder
{
    private sealed record CategorySeed(string Code, string Name, string EnglishName, AccountType Type, CashFlowActivity Activity, int Sort);

    private sealed record AccountSeed(
        string Code,
        string Name,
        string EnglishName,
        AccountType Type,
        string? Parent,
        string? Category,
        bool Postable = true,
        bool IsCash = false,
        bool Manual = true,
        bool CostCenter = false);

    private static readonly CategorySeed[] Categories =
    {
        new("CASH", "النقدية وما في حكمها", "Cash and cash equivalents", AccountType.Assets, CashFlowActivity.None, 10),
        new("RECEIVABLES", "الذمم المدينة", "Receivables", AccountType.Assets, CashFlowActivity.Operating, 20),
        new("OTHER_CA", "أصول متداولة أخرى", "Other current assets", AccountType.Assets, CashFlowActivity.Operating, 30),
        new("FIXED_ASSETS", "الأصول الثابتة", "Fixed assets", AccountType.Assets, CashFlowActivity.Investing, 40),
        new("CURRENT_LIAB", "الخصوم المتداولة", "Current liabilities", AccountType.Liabilities, CashFlowActivity.Operating, 50),
        new("LONG_LIAB", "الخصوم طويلة الأجل", "Long-term liabilities", AccountType.Liabilities, CashFlowActivity.Financing, 55),
        new("EQUITY", "حقوق الملكية", "Equity", AccountType.Equity, CashFlowActivity.Financing, 60),
        new("OP_REVENUE", "إيرادات التشغيل", "Operating revenue", AccountType.Revenue, CashFlowActivity.Operating, 70),
        new("OTHER_REVENUE", "إيرادات أخرى", "Other revenue", AccountType.Revenue, CashFlowActivity.Operating, 75),
        new("COST_OF_REVENUE", "تكلفة الإيرادات", "Cost of revenue", AccountType.CostOfRevenue, CashFlowActivity.Operating, 80),
        new("OP_EXPENSES", "المصاريف التشغيلية", "Operating expenses", AccountType.Expenses, CashFlowActivity.Operating, 90),
        new("ADMIN_EXPENSES", "المصاريف العمومية والإدارية", "General and administrative expenses", AccountType.Expenses, CashFlowActivity.Operating, 95)
    };

    private static readonly AccountSeed[] Accounts =
    {
        new("1", "الأصول", "Assets", AccountType.Assets, null, null, Postable: false),
        new("11", "الأصول المتداولة", "Current assets", AccountType.Assets, "1", null, Postable: false),
        new("1101", "الصناديق", "Cashboxes", AccountType.Assets, "11", "CASH", IsCash: true, Manual: false),
        new("1102", "نقدية محوّلة بين الفروع", "Cash in transit between branches", AccountType.Assets, "11", "CASH", IsCash: true, Manual: false),
        new("1103", "البنوك", "Banks", AccountType.Assets, "11", "CASH", IsCash: true),
        new("1104", "نقدية العملاء قيد الإيداع", "Customer cash awaiting deposit", AccountType.Assets, "11", "CASH", IsCash: true, Manual: false),
        new("1111", "الذمم المدينة - العملاء", "Accounts receivable - customers", AccountType.Assets, "11", "RECEIVABLES", Manual: false),
        new("1112", "ذمم السائقين - مبالغ التحصيل", "COD receivable from drivers", AccountType.Assets, "11", "RECEIVABLES", Manual: false),
        new("1113", "سلف الموظفين", "Employee advances", AccountType.Assets, "11", "OTHER_CA"),
        new("1119", "حساب معلّق", "Suspense account", AccountType.Assets, "11", "OTHER_CA"),
        new("12", "الأصول الثابتة", "Fixed assets", AccountType.Assets, "1", null, Postable: false),
        new("1201", "المركبات", "Vehicles", AccountType.Assets, "12", "FIXED_ASSETS"),
        new("1202", "الأثاث والمعدات", "Furniture and equipment", AccountType.Assets, "12", "FIXED_ASSETS"),
        new("1209", "مجمع الإهلاك", "Accumulated depreciation", AccountType.Assets, "12", "FIXED_ASSETS"),

        new("2", "الخصوم", "Liabilities", AccountType.Liabilities, null, null, Postable: false),
        new("21", "الخصوم المتداولة", "Current liabilities", AccountType.Liabilities, "2", null, Postable: false),
        new("2101", "الموردون", "Supplier payables", AccountType.Liabilities, "21", "CURRENT_LIAB"),
        new("2102", "أمانات المرسلين - الدفع عند الاستلام", "COD liability to senders", AccountType.Liabilities, "21", "CURRENT_LIAB", Manual: false),
        new("2103", "دفعات مقدمة من العملاء", "Customer advances", AccountType.Liabilities, "21", "CURRENT_LIAB"),
        new("2104", "ضريبة القيمة المضافة المستحقة", "VAT / tax payable", AccountType.Liabilities, "21", "CURRENT_LIAB"),
        new("2105", "مصاريف مستحقة", "Accrued expenses", AccountType.Liabilities, "21", "CURRENT_LIAB"),
        new("22", "الخصوم طويلة الأجل", "Long-term liabilities", AccountType.Liabilities, "2", null, Postable: false),
        new("2201", "قروض طويلة الأجل", "Long-term loans", AccountType.Liabilities, "22", "LONG_LIAB"),

        new("3", "حقوق الملكية", "Equity", AccountType.Equity, null, null, Postable: false),
        new("3100", "رأس مال المالك", "Owner capital", AccountType.Equity, "3", "EQUITY"),
        new("3200", "الأرباح المحتجزة", "Retained earnings", AccountType.Equity, "3", "EQUITY"),
        new("3300", "أرصدة افتتاحية", "Opening balance equity", AccountType.Equity, "3", "EQUITY"),
        new("3400", "مسحوبات المالك", "Owner drawings", AccountType.Equity, "3", "EQUITY"),

        new("4", "الإيرادات", "Revenue", AccountType.Revenue, null, null, Postable: false),
        new("41", "إيرادات التشغيل", "Operating revenue", AccountType.Revenue, "4", null, Postable: false),
        new("4101", "إيرادات الشحن", "Shipping revenue", AccountType.Revenue, "41", "OP_REVENUE"),
        new("4102", "عمولات الدفع عند الاستلام", "COD commission revenue", AccountType.Revenue, "41", "OP_REVENUE"),
        new("4103", "إيرادات الخدمات الإضافية", "Additional service revenue", AccountType.Revenue, "41", "OP_REVENUE"),
        new("4104", "رسوم مخصومة من المرسلين", "Fees charged to senders", AccountType.Revenue, "41", "OP_REVENUE"),
        new("42", "إيرادات أخرى", "Other revenue", AccountType.Revenue, "4", null, Postable: false),
        new("4201", "إيرادات متنوعة", "Miscellaneous income", AccountType.Revenue, "42", "OTHER_REVENUE"),

        new("5", "تكلفة الإيرادات", "Cost of revenue", AccountType.CostOfRevenue, null, null, Postable: false),
        new("5101", "أجور التوصيل والسائقين المتعاقدين", "Delivery and contracted drivers", AccountType.CostOfRevenue, "5", "COST_OF_REVENUE"),
        new("5102", "شحن عبر شركات أخرى", "Subcontracted shipping", AccountType.CostOfRevenue, "5", "COST_OF_REVENUE"),

        new("6", "المصاريف", "Expenses", AccountType.Expenses, null, null, Postable: false),
        new("61", "المصاريف التشغيلية", "Operating expenses", AccountType.Expenses, "6", null, Postable: false),
        new("6101", "الوقود", "Fuel expense", AccountType.Expenses, "61", "OP_EXPENSES"),
        new("6102", "صيانة المركبات", "Vehicle maintenance expense", AccountType.Expenses, "61", "OP_EXPENSES"),
        new("6103", "رسوم طرق ومواقف", "Tolls and parking", AccountType.Expenses, "61", "OP_EXPENSES"),
        new("6104", "تعويضات المرسلين", "Sender compensation", AccountType.Expenses, "61", "OP_EXPENSES"),
        new("6105", "عجز عهدة السائقين", "Driver cash shortage", AccountType.Expenses, "61", "OP_EXPENSES"),
        new("6106", "فروقات النقدية", "Cash over and short", AccountType.Expenses, "61", "OP_EXPENSES"),
        new("6107", "المبالغ المستردة للعملاء", "Refund expense", AccountType.Expenses, "61", "OP_EXPENSES"),
        new("62", "المصاريف العمومية والإدارية", "General and administrative expenses", AccountType.Expenses, "6", null, Postable: false),
        new("6201", "الرواتب والأجور", "Salaries expense", AccountType.Expenses, "62", "ADMIN_EXPENSES"),
        new("6202", "الإيجارات", "Rent expense", AccountType.Expenses, "62", "ADMIN_EXPENSES"),
        new("6203", "كهرباء ومياه واتصالات", "Utilities", AccountType.Expenses, "62", "ADMIN_EXPENSES"),
        new("6204", "قرطاسية ومستلزمات مكتبية", "Office supplies", AccountType.Expenses, "62", "ADMIN_EXPENSES"),
        new("6205", "عمولات ومصاريف بنكية", "Bank charges", AccountType.Expenses, "62", "ADMIN_EXPENSES"),
        new("6209", "مصاريف عامة أخرى", "Other general expenses", AccountType.Expenses, "62", "ADMIN_EXPENSES"),
        new("6210", "مصروف الإهلاك", "Depreciation expense", AccountType.Expenses, "62", "ADMIN_EXPENSES")
    };

    /// <summary>The general posting rule of each role (account code).</summary>
    public static readonly IReadOnlyDictionary<string, string> RoleAccounts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [R.CashOnHand] = "1101",
        [R.CashInTransit] = "1102",
        [R.Bank] = "1103",
        [R.CashClearing] = "1104",
        [R.AccountsReceivable] = "1111",
        [R.DriverCustody] = "1112",
        [R.EmployeeAdvances] = "1113",
        [R.Suspense] = "1119",
        [R.AccountsPayable] = "2101",
        [R.CodPayableToSenders] = "2102",
        [R.CustomerAdvances] = "2103",
        [R.TaxPayable] = "2104",
        [R.OpeningBalanceEquity] = "3300",
        [R.ShippingRevenue] = "4101",
        [R.CodCommissionRevenue] = "4102",
        [R.AdditionalServiceRevenue] = "4103",
        [R.OtherFeesRevenue] = "4104",
        [R.OtherIncome] = "4201",
        [R.SenderCompensationExpense] = "6104",
        [R.DriverShortageLoss] = "6105",
        [R.CashOverShort] = "6106",
        [R.CustomerRefund] = "6107",
        [R.Expense] = "6209"
    };

    /// <summary>Expense rules per expense category code (the demo categories of step 3).</summary>
    private static readonly IReadOnlyDictionary<string, string> ExpenseCategoryAccounts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["FUEL"] = "6101",
        ["MAINT"] = "6102",
        ["TOLLS"] = "6103",
        ["SALARY"] = "6201",
        ["RENT"] = "6202",
        ["UTIL"] = "6203",
        ["OFFICE"] = "6204"
    };

    private static readonly (string Code, string Name, string EnglishName)[] CostCenters =
    {
        ("OPS", "العمليات والتوصيل", "Operations and delivery"),
        ("FLEET", "الأسطول", "Fleet"),
        ("SALES", "المبيعات وخدمة العملاء", "Sales and customer service"),
        ("ADMIN", "الإدارة العامة", "General administration")
    };

    private readonly AppDbContext _dbContext;
    private readonly FiscalPeriodService _fiscalPeriods;
    private readonly AccountingAccess _access;
    private readonly ILogger<ChartOfAccountsSeeder> _logger;

    public ChartOfAccountsSeeder(AppDbContext dbContext, FiscalPeriodService fiscalPeriods, AccountingAccess access, ILogger<ChartOfAccountsSeeder> logger)
    {
        _dbContext = dbContext;
        _fiscalPeriods = fiscalPeriods;
        _access = access;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.IsRelational() &&
            (await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            _logger.LogWarning("Chart of accounts skipped: there are pending migrations");
            return 0;
        }

        await AccountingDatabaseObjects.EnsureAsync(_dbContext, cancellationToken);

        var created = 0;
        if (!await _dbContext.Accounts.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            created += await SeedChartAsync(cancellationToken);
            _logger.LogInformation("Chart of accounts created: {Count} accounts, posting rules and cost centers", Accounts.Length);
        }

        var rules = await SeedMissingRulesAsync(cancellationToken);
        if (rules > 0)
        {
            _logger.LogInformation("Accounting posting rules added for {Count} role(s) without a rule", rules);
        }

        var years = await SeedFiscalYearsAsync(cancellationToken);
        if (years > 0)
        {
            _logger.LogInformation("Fiscal years created: {Count}", years);
        }

        return created + rules + years;
    }

    private async Task<int> SeedChartAsync(CancellationToken ct)
    {
        var categories = Categories.ToDictionary(x => x.Code, x => new AccountCategory
        {
            Code = x.Code,
            ArabicName = x.Name,
            EnglishName = x.EnglishName,
            AccountType = x.Type,
            CashFlowActivity = x.Activity,
            SortOrder = x.Sort,
            IsSystem = true,
            IsActive = true
        });
        _dbContext.AccountCategories.AddRange(categories.Values);

        var accounts = new Dictionary<string, Account>();
        foreach (var seed in Accounts)
        {
            var parent = seed.Parent is null ? null : accounts[seed.Parent];
            var account = new Account
            {
                Code = seed.Code,
                ArabicName = seed.Name,
                EnglishName = seed.EnglishName,
                AccountType = seed.Type,
                NormalBalance = AccountRules.NormalBalanceOf(seed.Type),
                AccountCategory = seed.Category is null ? null : categories[seed.Category],
                ParentAccount = parent,
                Level = parent is null ? 1 : parent.Level + 1,
                IsPostable = seed.Postable,
                IsSystem = true,
                IsCashAccount = seed.IsCash,
                AllowManualPosting = seed.Manual,
                RequiresCostCenter = seed.CostCenter,
                IsActive = true
            };
            accounts[seed.Code] = account;
            _dbContext.Accounts.Add(account);
        }

        foreach (var (role, code) in RoleAccounts)
        {
            _dbContext.AccountingPostingRules.Add(new AccountingPostingRule
            {
                AccountRole = role,
                Account = accounts[code],
                Description = $"القاعدة العامة: {AccountRoleNames.Arabic(role)}",
                IsActive = true
            });
        }

        var expenseCategories = await _dbContext.ExpenseCategories.AsNoTracking()
            .Select(x => new { x.Id, x.Code, x.ArabicName })
            .ToListAsync(ct);
        foreach (var category in expenseCategories)
        {
            if (ExpenseCategoryAccounts.TryGetValue(category.Code, out var code))
            {
                _dbContext.AccountingPostingRules.Add(new AccountingPostingRule
                {
                    AccountRole = R.Expense,
                    Account = accounts[code],
                    ExpenseCategoryId = category.Id,
                    Description = $"مصروفات فئة {category.ArabicName}",
                    IsActive = true
                });
            }
        }

        foreach (var (code, name, englishName) in CostCenters)
        {
            if (!await _dbContext.AccountingDimensions.IgnoreQueryFilters().AnyAsync(x => x.DimensionType == DimensionType.CostCenter && x.Code == code, ct))
            {
                _dbContext.AccountingDimensions.Add(new AccountingDimension
                {
                    DimensionType = DimensionType.CostCenter,
                    Code = code,
                    ArabicName = name,
                    EnglishName = englishName,
                    IsActive = true
                });
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        return Accounts.Length;
    }

    private async Task<int> SeedMissingRulesAsync(CancellationToken ct)
    {
        var withRule = await _dbContext.AccountingPostingRules.AsNoTracking().Where(x => x.IsActive).Select(x => x.AccountRole).Distinct().ToListAsync(ct);
        var missing = R.All.Where(role => !withRule.Contains(role, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count == 0)
        {
            return 0;
        }

        var codes = missing.Where(RoleAccounts.ContainsKey).Select(x => RoleAccounts[x]).Distinct().ToList();
        var accounts = await _dbContext.Accounts.AsNoTracking()
            .Where(x => codes.Contains(x.Code) && x.IsPostable && x.IsActive)
            .ToDictionaryAsync(x => x.Code, x => x.Id, ct);

        var added = 0;
        foreach (var role in missing)
        {
            if (RoleAccounts.TryGetValue(role, out var code) && accounts.TryGetValue(code, out var accountId))
            {
                _dbContext.AccountingPostingRules.Add(new AccountingPostingRule
                {
                    AccountRole = role,
                    AccountId = accountId,
                    Description = $"القاعدة العامة: {AccountRoleNames.Arabic(role)}",
                    IsActive = true
                });
                added++;
            }
        }

        if (added > 0)
        {
            await _dbContext.SaveChangesAsync(ct);
        }

        return added;
    }

    private async Task<int> SeedFiscalYearsAsync(CancellationToken ct)
    {
        var currentYear = _access.Today.Year;
        var oldestPending = await _dbContext.OutboxMessages.AsNoTracking()
            .Where(x => x.ProcessedAtUtc == null)
            .OrderBy(x => x.OccurredAtUtc)
            .Select(x => (DateTime?)x.OccurredAtUtc)
            .FirstOrDefaultAsync(ct);
        var firstYear = oldestPending.HasValue ? Math.Min(currentYear, _access.Calendar.BusinessDate(oldestPending.Value).Year) : currentYear;

        var created = 0;
        for (var year = firstYear; year <= currentYear; year++)
        {
            var start = BusinessCalendar.AsDate(new DateTime(year, 1, 1));
            var end = BusinessCalendar.AsDate(new DateTime(year, 12, 31));
            if (await _dbContext.FiscalYears.AnyAsync(x => x.StartDate <= end && x.EndDate >= start, ct))
            {
                continue;
            }

            await _fiscalPeriods.CreateYearAsync(new CreateFiscalYearRequest { StartDate = start, EndDate = end }, audit: false, ct);
            created++;
        }

        return created;
    }
}
