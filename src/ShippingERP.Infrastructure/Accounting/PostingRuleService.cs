using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Accounting;
using ShippingERP.Domain.Entities.Accounting;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Identity;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Accounting;

/// <summary>
/// Automatic posting rules (قواعد الترحيل الآلي, step 7): which general ledger account each account ROLE of the
/// operational events uses, optionally per branch / currency / cashbox / bank account / expense category.
/// A change applies to the events posted after it; posted entries never change.
/// </summary>
public sealed class PostingRuleService : IPostingRuleService
{
    private const string EntityName = "AccountingPostingRule";

    private readonly AppDbContext _dbContext;
    private readonly PostingAccountResolver _resolver;
    private readonly AuditLogWriter _auditLog;

    public PostingRuleService(AppDbContext dbContext, PostingAccountResolver resolver, AuditLogWriter auditLog)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _auditLog = auditLog;
    }

    private AppDbContext Db => _dbContext;

    public async Task<PagedResult<PostingRuleDto>> GetPagedAsync(PostingRuleListQuery query, CancellationToken cancellationToken)
    {
        var rows = Db.AccountingPostingRules.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.AccountRole))
        {
            var role = query.AccountRole.Trim();
            rows = rows.Where(x => x.AccountRole == role);
        }

        if (query.AccountId.HasValue)
        {
            var accountId = query.AccountId.Value;
            rows = rows.Where(x => x.AccountId == accountId);
        }

        if (query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            rows = rows.Where(x => x.BranchId == branchId);
        }

        if (query.IsActive.HasValue)
        {
            var active = query.IsActive.Value;
            rows = rows.Where(x => x.IsActive == active);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            rows = rows.Where(x => x.AccountRole.Contains(search) || (x.Description != null && x.Description.Contains(search)) || x.Account.Code.StartsWith(search));
        }

        var total = await rows.CountAsync(cancellationToken);
        var byPriority = string.Equals(query.SortBy, "priority", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<AccountingPostingRule> ordered = byPriority
            ? (query.IsDescending() ? rows.OrderByDescending(x => x.Priority) : rows.OrderBy(x => x.Priority))
            : (query.IsDescending() ? rows.OrderByDescending(x => x.AccountRole) : rows.OrderBy(x => x.AccountRole));

        var items = await ordered.ThenBy(x => x.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return new PagedResult<PostingRuleDto>(await EnrichAsync(items, cancellationToken), query.PageNumber, query.PageSize, total);
    }

    public async Task<PostingRuleDto> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var dto = await Db.AccountingPostingRules.AsNoTracking().Where(x => x.Id == id).Select(Projection).FirstOrDefaultAsync(cancellationToken)
                  ?? throw new NotFoundException("قاعدة الترحيل", id);
        return (await EnrichAsync(new[] { dto }, cancellationToken))[0];
    }

    public async Task<PostingRuleDto> CreateAsync(SavePostingRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = new AccountingPostingRule();
        await ApplyAsync(rule, request, cancellationToken);
        Db.AccountingPostingRules.Add(rule);
        await Db.SaveChangesAsync(cancellationToken);

        Audit(AuditAction.PostingRuleSaved, rule.Id, new { action = "create", rule.AccountRole, rule.AccountId, rule.BranchId, rule.CurrencyId, rule.CashboxId, rule.BankAccountId, rule.ExpenseCategoryId });
        await Db.SaveChangesAsync(cancellationToken);
        _resolver.Reset();
        return await GetByIdAsync(rule.Id, cancellationToken);
    }

    public async Task<PostingRuleDto> UpdateAsync(long id, SavePostingRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await Db.AccountingPostingRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("قاعدة الترحيل", id);
        var before = new { rule.AccountRole, rule.AccountId, rule.IsActive };

        await ApplyAsync(rule, request, cancellationToken);
        Audit(AuditAction.PostingRuleSaved, rule.Id, new { action = "update", before, after = new { rule.AccountRole, rule.AccountId, rule.IsActive } });
        await Db.SaveChangesAsync(cancellationToken);
        _resolver.Reset();
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var rule = await Db.AccountingPostingRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("قاعدة الترحيل", id);

        if (IsGeneral(rule) && !await Db.AccountingPostingRules.AnyAsync(
                x => x.Id != id && x.IsActive && x.AccountRole == rule.AccountRole &&
                     x.BranchId == null && x.CurrencyId == null && x.CashboxId == null && x.BankAccountId == null && x.ExpenseCategoryId == null,
                cancellationToken))
        {
            throw new BusinessRuleException(
                $"هذه القاعدة العامة الوحيدة للدور «{AccountRoleNames.Arabic(rule.AccountRole)}»؛ حذفها يوقف الترحيل الآلي. غيّر حسابها بدلاً من ذلك.");
        }

        Db.AccountingPostingRules.Remove(rule);
        Audit(AuditAction.PostingRuleDeleted, rule.Id, new { rule.AccountRole, rule.AccountId });
        await Db.SaveChangesAsync(cancellationToken);
        _resolver.Reset();
    }

    public IReadOnlyList<AccountRoleDto> GetRoles() =>
        AccountRoles.All.Select(role => new AccountRoleDto { Role = role, ArabicName = AccountRoleNames.Arabic(role) }).ToList();

    public async Task<ResolvedPostingAccountDto> ResolveAsync(ResolvePostingAccountQuery query, CancellationToken cancellationToken)
    {
        var role = AccountRoleNames.Canonical(query.AccountRole);
        var resolved = await _resolver.ResolveAsync(
            new PostingScope(role, query.BranchId, query.CurrencyId, query.CashboxId, query.BankAccountId, query.ExpenseCategoryId), cancellationToken);

        return new ResolvedPostingAccountDto
        {
            AccountRole = role,
            AccountId = resolved.AccountId,
            AccountCode = resolved.Code,
            AccountName = resolved.ArabicName,
            RuleId = resolved.RuleId
        };
    }

    private async Task ApplyAsync(AccountingPostingRule rule, SavePostingRuleRequest request, CancellationToken cancellationToken)
    {
        var role = AccountRoleNames.Canonical(request.AccountRole);

        var account = await Db.Accounts.AsNoTracking()
                          .Where(x => x.Id == request.AccountId)
                          .Select(x => new { x.Code, x.IsActive, x.IsPostable, x.CurrencyId })
                          .FirstOrDefaultAsync(cancellationToken)
                      ?? throw AccountingAccess.Invalid("accountId", "الحساب غير موجود.");

        if (!account.IsActive || !account.IsPostable)
        {
            throw AccountingAccess.Invalid("accountId", $"الحساب {account.Code} يجب أن يكون حساباً فرعياً مفعّلاً.");
        }

        if (account.CurrencyId.HasValue && request.CurrencyId != account.CurrencyId)
        {
            throw AccountingAccess.Invalid("currencyId", $"الحساب {account.Code} مقيّد بعملة؛ حدّد هذه العملة في القاعدة.");
        }

        if (request.BranchId.HasValue && !await Db.Branches.AnyAsync(x => x.Id == request.BranchId.Value, cancellationToken))
        {
            throw AccountingAccess.Invalid("branchId", "الفرع غير موجود.");
        }

        if (request.CurrencyId.HasValue && !await Db.Currencies.AnyAsync(x => x.Id == request.CurrencyId.Value, cancellationToken))
        {
            throw AccountingAccess.Invalid("currencyId", "العملة غير موجودة.");
        }

        if (request.CashboxId.HasValue && !await Db.Cashboxes.AnyAsync(x => x.Id == request.CashboxId.Value, cancellationToken))
        {
            throw AccountingAccess.Invalid("cashboxId", "الصندوق غير موجود.");
        }

        if (request.BankAccountId.HasValue && !await Db.BankAccounts.AnyAsync(x => x.Id == request.BankAccountId.Value, cancellationToken))
        {
            throw AccountingAccess.Invalid("bankAccountId", "الحساب البنكي غير موجود.");
        }

        if (request.ExpenseCategoryId.HasValue && !await Db.ExpenseCategories.AnyAsync(x => x.Id == request.ExpenseCategoryId.Value, cancellationToken))
        {
            throw AccountingAccess.Invalid("expenseCategoryId", "فئة المصروف غير موجودة.");
        }

        var duplicate = await Db.AccountingPostingRules.AnyAsync(
            x => x.Id != rule.Id && x.IsActive && x.AccountRole == role && x.Priority == request.Priority &&
                 x.BranchId == request.BranchId && x.CurrencyId == request.CurrencyId && x.CashboxId == request.CashboxId &&
                 x.BankAccountId == request.BankAccountId && x.ExpenseCategoryId == request.ExpenseCategoryId,
            cancellationToken);
        if (duplicate && request.IsActive)
        {
            throw new ConflictException("توجد قاعدة مفعّلة بنفس الدور والنطاق والأولوية؛ عدّلها بدلاً من إضافة قاعدة جديدة.");
        }

        rule.AccountRole = role;
        rule.AccountId = request.AccountId;
        rule.BranchId = request.BranchId;
        rule.CurrencyId = request.CurrencyId;
        rule.CashboxId = request.CashboxId;
        rule.BankAccountId = request.BankAccountId;
        rule.ExpenseCategoryId = request.ExpenseCategoryId;
        rule.Priority = request.Priority;
        rule.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        rule.IsActive = request.IsActive;
    }

    private static bool IsGeneral(AccountingPostingRule rule) =>
        rule.IsActive && rule.BranchId is null && rule.CurrencyId is null && rule.CashboxId is null && rule.BankAccountId is null && rule.ExpenseCategoryId is null;

    private static readonly Expression<Func<AccountingPostingRule, PostingRuleDto>> Projection = x => new PostingRuleDto
    {
        Id = x.Id,
        AccountRole = x.AccountRole,
        AccountId = x.AccountId,
        AccountCode = x.Account.Code,
        AccountName = x.Account.ArabicName,
        BranchId = x.BranchId,
        CurrencyId = x.CurrencyId,
        CashboxId = x.CashboxId,
        BankAccountId = x.BankAccountId,
        ExpenseCategoryId = x.ExpenseCategoryId,
        Priority = x.Priority,
        Description = x.Description,
        IsActive = x.IsActive
    };

    /// <summary>Adds the scope names (one query per scope type for the whole page) and the specificity.</summary>
    private async Task<List<PostingRuleDto>> EnrichAsync(IReadOnlyList<PostingRuleDto> rules, CancellationToken cancellationToken)
    {
        static List<long> Ids(IEnumerable<long?> values) => values.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();

        var branchIds = Ids(rules.Select(x => x.BranchId));
        var currencyIds = Ids(rules.Select(x => x.CurrencyId));
        var cashboxIds = Ids(rules.Select(x => x.CashboxId));
        var bankIds = Ids(rules.Select(x => x.BankAccountId));
        var categoryIds = Ids(rules.Select(x => x.ExpenseCategoryId));

        var branches = branchIds.Count == 0 ? new Dictionary<long, string>() : await Db.Branches.AsNoTracking().IgnoreQueryFilters()
            .Where(x => branchIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ArabicName, cancellationToken);
        var currencies = currencyIds.Count == 0 ? new Dictionary<long, string>() : await Db.Currencies.AsNoTracking().IgnoreQueryFilters()
            .Where(x => currencyIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);
        var cashboxes = cashboxIds.Count == 0 ? new Dictionary<long, string>() : await Db.Cashboxes.AsNoTracking().IgnoreQueryFilters()
            .Where(x => cashboxIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ArabicName, cancellationToken);
        var banks = bankIds.Count == 0 ? new Dictionary<long, string>() : await Db.BankAccounts.AsNoTracking().IgnoreQueryFilters()
            .Where(x => bankIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ArabicName, cancellationToken);
        var categories = categoryIds.Count == 0 ? new Dictionary<long, string>() : await Db.ExpenseCategories.AsNoTracking().IgnoreQueryFilters()
            .Where(x => categoryIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ArabicName, cancellationToken);

        static string? Name(Dictionary<long, string> names, long? id) => id.HasValue && names.TryGetValue(id.Value, out var name) ? name : null;

        return rules.Select(dto => new PostingRuleDto
        {
            Id = dto.Id,
            AccountRole = dto.AccountRole,
            AccountId = dto.AccountId,
            AccountCode = dto.AccountCode,
            AccountName = dto.AccountName,
            BranchId = dto.BranchId,
            BranchName = Name(branches, dto.BranchId),
            CurrencyId = dto.CurrencyId,
            CurrencyCode = Name(currencies, dto.CurrencyId),
            CashboxId = dto.CashboxId,
            CashboxName = Name(cashboxes, dto.CashboxId),
            BankAccountId = dto.BankAccountId,
            BankAccountName = Name(banks, dto.BankAccountId),
            ExpenseCategoryId = dto.ExpenseCategoryId,
            ExpenseCategoryName = Name(categories, dto.ExpenseCategoryId),
            Priority = dto.Priority,
            Specificity = (dto.BranchId.HasValue ? 1 : 0) + (dto.CurrencyId.HasValue ? 1 : 0) + (dto.CashboxId.HasValue ? 1 : 0) +
                          (dto.BankAccountId.HasValue ? 1 : 0) + (dto.ExpenseCategoryId.HasValue ? 1 : 0),
            Description = dto.Description,
            IsActive = dto.IsActive
        }).ToList();
    }

    private void Audit(AuditAction action, long id, object details) =>
        _auditLog.Add(action, entityName: EntityName, entityId: id.ToString(CultureInfo.InvariantCulture), details: details);
}
