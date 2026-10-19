using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Accounting;
using ShippingERP.Domain.Entities.Accounting;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Cash;
using ShippingERP.Infrastructure.Identity;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Accounting;

/// <summary>
/// Chart of accounts (دليل الحسابات), account categories and analytical dimensions (step 7).
///
/// Rules: a child has the type of its parent and a header parent; a header account never receives lines; the type
/// and the postable flag of an account with journal lines never change; system accounts (seeded, mapped by the
/// posting rules) are never deleted; nothing used is deleted (soft delete otherwise).
/// </summary>
public sealed class ChartOfAccountsService : IChartOfAccountsService
{
    private const int MaxLevel = 10;

    private static readonly Dictionary<string, Expression<Func<Account, object?>>> AccountSorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = x => x.Code,
        ["arabicName"] = x => x.ArabicName,
        ["accountType"] = x => x.AccountType
    };

    private readonly AppDbContext _dbContext;
    private readonly AccountingAccess _access;
    private readonly AuditLogWriter _auditLog;

    public ChartOfAccountsService(AppDbContext dbContext, AccountingAccess access, AuditLogWriter auditLog)
    {
        _dbContext = dbContext;
        _access = access;
        _auditLog = auditLog;
    }

    private AppDbContext Db => _dbContext;

    // ------------------------------------------------------------------ accounts

    public async Task<PagedResult<AccountDto>> GetAccountsAsync(AccountListQuery query, CancellationToken cancellationToken)
    {
        var rows = Db.Accounts.AsNoTracking();

        if (query.AccountType.HasValue)
        {
            var type = query.AccountType.Value;
            rows = rows.Where(x => x.AccountType == type);
        }

        if (query.AccountCategoryId.HasValue)
        {
            var categoryId = query.AccountCategoryId.Value;
            rows = rows.Where(x => x.AccountCategoryId == categoryId);
        }

        if (query.ParentAccountId.HasValue)
        {
            var parentId = query.ParentAccountId.Value;
            rows = rows.Where(x => x.ParentAccountId == parentId);
        }

        if (query.IsPostable.HasValue)
        {
            var postable = query.IsPostable.Value;
            rows = rows.Where(x => x.IsPostable == postable);
        }

        if (query.IsActive.HasValue)
        {
            var active = query.IsActive.Value;
            rows = rows.Where(x => x.IsActive == active);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.Code.StartsWith(search) || x.ArabicName.Contains(search) || (x.EnglishName != null && x.EnglishName.Contains(search)));
        }

        var total = await rows.CountAsync(cancellationToken);
        IOrderedQueryable<Account> ordered = !string.IsNullOrWhiteSpace(query.SortBy) && AccountSorts.TryGetValue(query.SortBy, out var sort)
            ? (query.IsDescending() ? rows.OrderByDescending(sort) : rows.OrderBy(sort))
            : rows.OrderBy(x => x.Code);

        var items = await ordered.ThenBy(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(AccountingProjections.AccountItem)
            .ToListAsync(cancellationToken);

        return new PagedResult<AccountDto>(items.Select(x => x.WithNames()).ToList(), query.PageNumber, query.PageSize, total);
    }

    public async Task<IReadOnlyList<AccountTreeNodeDto>> GetTreeAsync(AccountTreeQuery query, CancellationToken cancellationToken)
    {
        var accounts = await Db.Accounts.AsNoTracking()
            .Where(x => query.IncludeInactive || x.IsActive)
            .OrderBy(x => x.Code)
            .Select(x => new { x.Id, x.Code, x.ArabicName, x.AccountType, x.Level, x.IsPostable, x.IsActive, x.ParentAccountId })
            .ToListAsync(cancellationToken);

        Dictionary<long, decimal>? balances = null;
        if (query.IncludeBalances)
        {
            var asOf = BusinessCalendar.AsDate(query.AsOfDate ?? _access.Today);
            var branchId = _access.ForcedBranchId;
            var lines = Db.JournalEntryLines.AsNoTracking()
                .Where(x => (x.JournalEntry.Status == JournalEntryStatus.Posted || x.JournalEntry.Status == JournalEntryStatus.Reversed) &&
                            x.JournalEntry.EntryDate <= asOf);
            if (branchId.HasValue)
            {
                lines = lines.Where(x => x.BranchId == branchId.Value);
            }

            balances = (await lines
                    .GroupBy(x => x.AccountId)
                    .Select(g => new { AccountId = g.Key, Net = g.Sum(x => x.DebitBase) - g.Sum(x => x.CreditBase) })
                    .ToListAsync(cancellationToken))
                .ToDictionary(x => x.AccountId, x => x.Net);
        }

        var nodes = accounts.ToDictionary(x => x.Id, x => new AccountTreeNodeDto
        {
            Id = x.Id,
            Code = x.Code,
            ArabicName = x.ArabicName,
            AccountType = x.AccountType,
            AccountTypeName = AccountingNames.Arabic(x.AccountType),
            Level = x.Level,
            IsPostable = x.IsPostable,
            IsActive = x.IsActive
        });

        var roots = new List<AccountTreeNodeDto>();
        foreach (var account in accounts)
        {
            if (account.ParentAccountId.HasValue && nodes.TryGetValue(account.ParentAccountId.Value, out var parent))
            {
                parent.Children.Add(nodes[account.Id]);
            }
            else
            {
                roots.Add(nodes[account.Id]);
            }
        }

        if (balances is not null)
        {
            foreach (var root in roots)
            {
                FillBalance(root, balances);
            }
        }

        return roots;
    }

    /// <summary>Natural balance of a node: its own lines plus all its children (debit − credit, signed by the type).</summary>
    private static decimal FillBalance(AccountTreeNodeDto node, IReadOnlyDictionary<long, decimal> netByAccount)
    {
        var net = netByAccount.TryGetValue(node.Id, out var own) ? own : 0m;
        foreach (var child in node.Children)
        {
            FillBalance(child, netByAccount);
            net += child.Balance.HasValue ? ToNet(child.AccountType, child.Balance.Value) : 0m;
        }

        node.Balance = AccountRules.NormalBalanceOf(node.AccountType) == NormalBalance.Debit ? net : -net;
        return net;
    }

    private static decimal ToNet(AccountType type, decimal natural) =>
        AccountRules.NormalBalanceOf(type) == NormalBalance.Debit ? natural : -natural;

    public async Task<AccountDto> GetAccountAsync(long id, CancellationToken cancellationToken)
    {
        var dto = await Db.Accounts.AsNoTracking().Where(x => x.Id == id).Select(AccountingProjections.AccountItem).FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException("الحساب", id);
        return dto.WithNames();
    }

    public async Task<AccountDto> CreateAccountAsync(SaveAccountRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        if (await Db.Accounts.IgnoreQueryFilters().AnyAsync(x => x.Code == code, cancellationToken))
        {
            throw new ConflictException($"رقم الحساب {code} مستخدم من قبل.");
        }

        var account = new Account { Code = code };
        await ApplyAsync(account, request, isNew: true, cancellationToken);

        Db.Accounts.Add(account);
        await Db.SaveChangesAsync(cancellationToken);

        Audit(AuditAction.AccountSaved, "Account", account.Id, new { action = "create", account.Code, account.ArabicName, account.AccountType, account.ParentAccountId });
        await Db.SaveChangesAsync(cancellationToken);
        return await GetAccountAsync(account.Id, cancellationToken);
    }

    public async Task<AccountDto> UpdateAccountAsync(long id, SaveAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await Db.Accounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("الحساب", id);

        var code = request.Code.Trim();
        if (code != account.Code && await Db.Accounts.IgnoreQueryFilters().AnyAsync(x => x.Code == code && x.Id != id, cancellationToken))
        {
            throw new ConflictException($"رقم الحساب {code} مستخدم من قبل.");
        }

        var hasLines = await Db.JournalEntryLines.AnyAsync(x => x.AccountId == id, cancellationToken);
        var hasChildren = await Db.Accounts.AnyAsync(x => x.ParentAccountId == id, cancellationToken);

        if (request.AccountType != account.AccountType && (account.IsSystem || hasLines || hasChildren))
        {
            throw new BusinessRuleException($"لا يمكن تغيير نوع الحساب {account.Code}: حساب نظام أو له قيود أو حسابات فرعية.");
        }

        if (!request.IsPostable && account.IsPostable && hasLines)
        {
            throw new BusinessRuleException($"الحساب {account.Code} له قيود؛ لا يتحول إلى حساب رئيسي.");
        }

        if (request.IsPostable && !account.IsPostable && hasChildren)
        {
            throw new BusinessRuleException($"الحساب {account.Code} له حسابات فرعية؛ لا يتحول إلى حساب فرعي.");
        }

        if (request.ParentAccountId != account.ParentAccountId && hasChildren)
        {
            throw new BusinessRuleException($"الحساب {account.Code} له حسابات فرعية؛ انقلها أولاً ثم غيّر الحساب الأب.");
        }

        if (request.CurrencyId != account.CurrencyId && request.CurrencyId.HasValue && hasLines &&
            await Db.JournalEntryLines.AnyAsync(x => x.AccountId == id && x.JournalEntry.CurrencyId != request.CurrencyId.Value, cancellationToken))
        {
            throw new BusinessRuleException($"للحساب {account.Code} قيود بعملات أخرى؛ لا يُقيّد بعملة واحدة.");
        }

        if (!request.IsActive && account.IsActive &&
            await Db.AccountingPostingRules.AnyAsync(x => x.AccountId == id && x.IsActive, cancellationToken))
        {
            throw new BusinessRuleException($"الحساب {account.Code} مستخدم في قواعد ترحيل مفعّلة؛ غيّر القواعد أولاً.");
        }

        var before = new { account.Code, account.ArabicName, account.AccountType, account.ParentAccountId, account.IsPostable, account.IsActive };
        account.Code = code;
        await ApplyAsync(account, request, isNew: false, cancellationToken);

        Audit(AuditAction.AccountSaved, "Account", account.Id, new
        {
            action = "update",
            before,
            after = new { account.Code, account.ArabicName, account.AccountType, account.ParentAccountId, account.IsPostable, account.IsActive }
        });
        await Db.SaveChangesAsync(cancellationToken);
        return await GetAccountAsync(id, cancellationToken);
    }

    public async Task DeleteAccountAsync(long id, CancellationToken cancellationToken)
    {
        var account = await Db.Accounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("الحساب", id);

        if (account.IsSystem)
        {
            throw new BusinessRuleException($"الحساب {account.Code} حساب نظام ولا يُحذف؛ يمكنك تعديل اسمه.");
        }

        if (await Db.Accounts.AnyAsync(x => x.ParentAccountId == id, cancellationToken))
        {
            throw new BusinessRuleException($"للحساب {account.Code} حسابات فرعية.");
        }

        if (await Db.JournalEntryLines.AnyAsync(x => x.AccountId == id, cancellationToken))
        {
            throw new BusinessRuleException($"للحساب {account.Code} قيود؛ أوقف تفعيله بدلاً من حذفه.");
        }

        if (await Db.AccountingPostingRules.IgnoreQueryFilters().AnyAsync(x => x.AccountId == id && !x.IsDeleted, cancellationToken) ||
            await Db.BankAccounts.AnyAsync(x => x.AccountId == id, cancellationToken))
        {
            throw new BusinessRuleException($"الحساب {account.Code} مستخدم في قواعد الترحيل أو الحسابات البنكية.");
        }

        Db.Accounts.Remove(account);
        Audit(AuditAction.AccountDeleted, "Account", account.Id, new { account.Code, account.ArabicName });
        await Db.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyAsync(Account account, SaveAccountRequest request, bool isNew, CancellationToken cancellationToken)
    {
        var level = 1;
        if (request.ParentAccountId.HasValue)
        {
            var parentId = request.ParentAccountId.Value;
            if (!isNew && parentId == account.Id)
            {
                throw AccountingAccess.Invalid("parentAccountId", "لا يكون الحساب أباً لنفسه.");
            }

            var parent = await Db.Accounts.AsNoTracking()
                             .Where(x => x.Id == parentId)
                             .Select(x => new { x.AccountType, x.IsPostable, x.Level, x.IsActive })
                             .FirstOrDefaultAsync(cancellationToken)
                         ?? throw AccountingAccess.Invalid("parentAccountId", "الحساب الأب غير موجود.");

            if (parent.IsPostable)
            {
                throw AccountingAccess.Invalid("parentAccountId", "الحساب الأب يجب أن يكون حساباً رئيسياً (غير قابل للقيد).");
            }

            if (parent.AccountType != request.AccountType)
            {
                throw AccountingAccess.Invalid("accountType", $"نوع الحساب يجب أن يطابق نوع الحساب الأب ({AccountingNames.Arabic(parent.AccountType)}).");
            }

            level = parent.Level + 1;
            if (level > MaxLevel)
            {
                throw AccountingAccess.Invalid("parentAccountId", $"عمق الدليل لا يزيد عن {MaxLevel} مستويات.");
            }
        }

        if (request.AccountCategoryId.HasValue)
        {
            var categoryId = request.AccountCategoryId.Value;
            var categoryType = await Db.AccountCategories.AsNoTracking().Where(x => x.Id == categoryId).Select(x => (AccountType?)x.AccountType)
                                   .FirstOrDefaultAsync(cancellationToken)
                               ?? throw AccountingAccess.Invalid("accountCategoryId", "التصنيف غير موجود.");
            if (categoryType != request.AccountType)
            {
                throw AccountingAccess.Invalid("accountCategoryId", "نوع التصنيف لا يطابق نوع الحساب.");
            }
        }

        if (request.CurrencyId.HasValue && !await Db.Currencies.AnyAsync(x => x.Id == request.CurrencyId.Value, cancellationToken))
        {
            throw AccountingAccess.Invalid("currencyId", "العملة غير موجودة.");
        }

        account.ArabicName = request.ArabicName.Trim();
        account.EnglishName = string.IsNullOrWhiteSpace(request.EnglishName) ? null : request.EnglishName.Trim();
        account.AccountType = request.AccountType;
        account.NormalBalance = AccountRules.NormalBalanceOf(request.AccountType);
        account.AccountCategoryId = request.AccountCategoryId;
        account.ParentAccountId = request.ParentAccountId;
        account.Level = level;
        account.IsPostable = request.IsPostable;
        account.IsCashAccount = request.IsCashAccount;
        account.AllowManualPosting = request.AllowManualPosting;
        account.RequiresCostCenter = request.RequiresCostCenter;
        account.CurrencyId = request.CurrencyId;
        account.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        account.IsActive = request.IsActive;
    }

    // ------------------------------------------------------------------ categories

    public async Task<IReadOnlyList<AccountCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        var rows = await Db.AccountCategories.AsNoTracking()
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .Select(x => new
            {
                x.Id, x.Code, x.ArabicName, x.EnglishName, x.AccountType, x.CashFlowActivity, x.SortOrder, x.IsSystem, x.IsActive,
                Count = x.Accounts.Count(a => !a.IsDeleted)
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new AccountCategoryDto
        {
            Id = x.Id,
            Code = x.Code,
            ArabicName = x.ArabicName,
            EnglishName = x.EnglishName,
            AccountType = x.AccountType,
            AccountTypeName = AccountingNames.Arabic(x.AccountType),
            CashFlowActivity = x.CashFlowActivity,
            CashFlowActivityName = AccountingNames.Arabic(x.CashFlowActivity),
            SortOrder = x.SortOrder,
            IsSystem = x.IsSystem,
            IsActive = x.IsActive,
            AccountCount = x.Count
        }).ToList();
    }

    public async Task<AccountCategoryDto> CreateCategoryAsync(SaveAccountCategoryRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await Db.AccountCategories.IgnoreQueryFilters().AnyAsync(x => x.Code == code, cancellationToken))
        {
            throw new ConflictException($"رمز التصنيف {code} مستخدم من قبل.");
        }

        var category = new AccountCategory { Code = code };
        ApplyCategory(category, request);
        Db.AccountCategories.Add(category);
        await Db.SaveChangesAsync(cancellationToken);

        Audit(AuditAction.AccountCategorySaved, "AccountCategory", category.Id, new { action = "create", category.Code, category.ArabicName });
        await Db.SaveChangesAsync(cancellationToken);
        return (await GetCategoriesAsync(cancellationToken)).First(x => x.Id == category.Id);
    }

    public async Task<AccountCategoryDto> UpdateCategoryAsync(long id, SaveAccountCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await Db.AccountCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("التصنيف", id);
        var code = request.Code.Trim().ToUpperInvariant();

        if (code != category.Code && await Db.AccountCategories.IgnoreQueryFilters().AnyAsync(x => x.Code == code && x.Id != id, cancellationToken))
        {
            throw new ConflictException($"رمز التصنيف {code} مستخدم من قبل.");
        }

        if (request.AccountType != category.AccountType && await Db.Accounts.AnyAsync(x => x.AccountCategoryId == id, cancellationToken))
        {
            throw new BusinessRuleException("للتصنيف حسابات؛ لا يتغير نوعه.");
        }

        category.Code = code;
        ApplyCategory(category, request);
        Audit(AuditAction.AccountCategorySaved, "AccountCategory", category.Id, new { action = "update", category.Code, category.ArabicName });
        await Db.SaveChangesAsync(cancellationToken);
        return (await GetCategoriesAsync(cancellationToken)).First(x => x.Id == id);
    }

    public async Task DeleteCategoryAsync(long id, CancellationToken cancellationToken)
    {
        var category = await Db.AccountCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("التصنيف", id);
        if (category.IsSystem)
        {
            throw new BusinessRuleException($"التصنيف {category.Code} تصنيف نظام ولا يُحذف.");
        }

        if (await Db.Accounts.AnyAsync(x => x.AccountCategoryId == id, cancellationToken))
        {
            throw new BusinessRuleException("للتصنيف حسابات؛ انقلها إلى تصنيف آخر أولاً.");
        }

        Db.AccountCategories.Remove(category);
        Audit(AuditAction.AccountCategorySaved, "AccountCategory", category.Id, new { action = "delete", category.Code });
        await Db.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyCategory(AccountCategory category, SaveAccountCategoryRequest request)
    {
        category.ArabicName = request.ArabicName.Trim();
        category.EnglishName = string.IsNullOrWhiteSpace(request.EnglishName) ? null : request.EnglishName.Trim();
        category.AccountType = request.AccountType;
        category.CashFlowActivity = request.CashFlowActivity;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
    }

    // ------------------------------------------------------------------ dimensions (cost centers...)

    public async Task<PagedResult<AccountingDimensionDto>> GetDimensionsAsync(AccountingDimensionListQuery query, CancellationToken cancellationToken)
    {
        var rows = Db.AccountingDimensions.AsNoTracking();

        if (query.DimensionType.HasValue)
        {
            var type = query.DimensionType.Value;
            rows = rows.Where(x => x.DimensionType == type);
        }

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.BranchId == branchId || x.BranchId == null);
        }

        if (query.IsActive.HasValue)
        {
            var active = query.IsActive.Value;
            rows = rows.Where(x => x.IsActive == active);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.Code.Contains(search) || x.ArabicName.Contains(search));
        }

        var total = await rows.CountAsync(cancellationToken);
        var byName = string.Equals(query.SortBy, "arabicName", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<AccountingDimension> ordered = byName
            ? (query.IsDescending() ? rows.OrderByDescending(x => x.ArabicName) : rows.OrderBy(x => x.ArabicName))
            : (query.IsDescending() ? rows.OrderByDescending(x => x.Code) : rows.OrderBy(x => x.Code));

        var items = await ordered.ThenBy(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(DimensionProjection)
            .ToListAsync(cancellationToken);

        return new PagedResult<AccountingDimensionDto>(items.Select(WithTypeName).ToList(), query.PageNumber, query.PageSize, total);
    }

    public async Task<AccountingDimensionDto> GetDimensionAsync(long id, CancellationToken cancellationToken)
    {
        var dto = await Db.AccountingDimensions.AsNoTracking().Where(x => x.Id == id).Select(DimensionProjection).FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException("البعد التحليلي", id);
        return WithTypeName(dto);
    }

    public async Task<AccountingDimensionDto> CreateDimensionAsync(SaveAccountingDimensionRequest request, CancellationToken cancellationToken)
    {
        var dimension = new AccountingDimension();
        await ApplyDimensionAsync(dimension, request, cancellationToken);
        Db.AccountingDimensions.Add(dimension);
        await Db.SaveChangesAsync(cancellationToken);

        Audit(AuditAction.AccountingDimensionSaved, "AccountingDimension", dimension.Id, new { action = "create", dimension.DimensionType, dimension.Code });
        await Db.SaveChangesAsync(cancellationToken);
        return await GetDimensionAsync(dimension.Id, cancellationToken);
    }

    public async Task<AccountingDimensionDto> UpdateDimensionAsync(long id, SaveAccountingDimensionRequest request, CancellationToken cancellationToken)
    {
        var dimension = await Db.AccountingDimensions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("البعد التحليلي", id);

        if (request.DimensionType != dimension.DimensionType && await Db.JournalEntryDimensions.AnyAsync(x => x.AccountingDimensionId == id, cancellationToken))
        {
            throw new BusinessRuleException("البعد مستخدم في قيود؛ لا يتغير نوعه.");
        }

        await ApplyDimensionAsync(dimension, request, cancellationToken);
        Audit(AuditAction.AccountingDimensionSaved, "AccountingDimension", dimension.Id, new { action = "update", dimension.DimensionType, dimension.Code });
        await Db.SaveChangesAsync(cancellationToken);
        return await GetDimensionAsync(id, cancellationToken);
    }

    public async Task DeleteDimensionAsync(long id, CancellationToken cancellationToken)
    {
        var dimension = await Db.AccountingDimensions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("البعد التحليلي", id);

        if (await Db.JournalEntryDimensions.AnyAsync(x => x.AccountingDimensionId == id, cancellationToken))
        {
            throw new BusinessRuleException("البعد مستخدم في قيود؛ أوقف تفعيله بدلاً من حذفه.");
        }

        if (await Db.AccountingDimensions.AnyAsync(x => x.ParentId == id, cancellationToken))
        {
            throw new BusinessRuleException("للبعد عناصر فرعية.");
        }

        Db.AccountingDimensions.Remove(dimension);
        Audit(AuditAction.AccountingDimensionSaved, "AccountingDimension", dimension.Id, new { action = "delete", dimension.Code });
        await Db.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyDimensionAsync(AccountingDimension dimension, SaveAccountingDimensionRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await Db.AccountingDimensions.IgnoreQueryFilters()
                .AnyAsync(x => x.DimensionType == request.DimensionType && x.Code == code && x.Id != dimension.Id, cancellationToken))
        {
            throw new ConflictException($"الرمز {code} مستخدم من قبل لنفس نوع البعد.");
        }

        if (request.BranchId.HasValue)
        {
            _access.BranchAccess.EnsureCanAccess(request.BranchId.Value);
            if (!await Db.Branches.AnyAsync(x => x.Id == request.BranchId.Value, cancellationToken))
            {
                throw AccountingAccess.Invalid("branchId", "الفرع غير موجود.");
            }
        }

        if (request.ParentId.HasValue)
        {
            var parentId = request.ParentId.Value;
            if (parentId == dimension.Id)
            {
                throw AccountingAccess.Invalid("parentId", "لا يكون البعد أباً لنفسه.");
            }

            var parentType = await Db.AccountingDimensions.AsNoTracking().Where(x => x.Id == parentId).Select(x => (DimensionType?)x.DimensionType)
                                 .FirstOrDefaultAsync(cancellationToken)
                             ?? throw AccountingAccess.Invalid("parentId", "البعد الأب غير موجود.");
            if (parentType != request.DimensionType)
            {
                throw AccountingAccess.Invalid("parentId", "البعد الأب من نوع آخر.");
            }
        }

        dimension.DimensionType = request.DimensionType;
        dimension.Code = code;
        dimension.ArabicName = request.ArabicName.Trim();
        dimension.EnglishName = string.IsNullOrWhiteSpace(request.EnglishName) ? null : request.EnglishName.Trim();
        dimension.BranchId = request.BranchId;
        dimension.ParentId = request.ParentId;
        dimension.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        dimension.IsActive = request.IsActive;
    }

    private static readonly Expression<Func<AccountingDimension, AccountingDimensionDto>> DimensionProjection = x => new AccountingDimensionDto
    {
        Id = x.Id,
        DimensionType = x.DimensionType,
        Code = x.Code,
        ArabicName = x.ArabicName,
        EnglishName = x.EnglishName,
        BranchId = x.BranchId,
        BranchName = x.Branch != null ? x.Branch.ArabicName : null,
        ParentId = x.ParentId,
        ParentName = x.Parent != null ? x.Parent.ArabicName : null,
        Description = x.Description,
        IsActive = x.IsActive
    };

    private static AccountingDimensionDto WithTypeName(AccountingDimensionDto dto) => new()
    {
        Id = dto.Id,
        DimensionType = dto.DimensionType,
        DimensionTypeName = AccountingNames.Arabic(dto.DimensionType),
        Code = dto.Code,
        ArabicName = dto.ArabicName,
        EnglishName = dto.EnglishName,
        BranchId = dto.BranchId,
        BranchName = dto.BranchName,
        ParentId = dto.ParentId,
        ParentName = dto.ParentName,
        Description = dto.Description,
        IsActive = dto.IsActive
    };

    private void Audit(AuditAction action, string entity, long id, object details) =>
        _auditLog.Add(action, entityName: entity, entityId: id.ToString(CultureInfo.InvariantCulture), details: details);
}
