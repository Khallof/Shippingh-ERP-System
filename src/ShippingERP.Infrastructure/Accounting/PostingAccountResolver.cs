using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Accounting;
using ShippingERP.Domain.Entities.Accounting;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Accounting;

/// <summary>A resolved posting account: the account and the rule that chose it (null = the bank account's own link).</summary>
public sealed record ResolvedAccount(long AccountId, string Code, string ArabicName, long? RuleId);

/// <summary>
/// Maps the account role of a posting line to a general ledger account (step 7):
///   1. Bank role with a bank account linked to its own GL account -> that account.
///   2. Otherwise the most specific matching AccountingPostingRule (PostingRuleMatcher).
/// The rules are loaded once per instance (one scope = one batch of events).
/// </summary>
public sealed class PostingAccountResolver
{
    private readonly AppDbContext _dbContext;
    private List<AccountingPostingRule>? _rules;
    private Dictionary<long, long?>? _bankAccounts;
    private Dictionary<long, (string Code, string Name, bool Usable)>? _accounts;

    public PostingAccountResolver(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Forgets the cached rules (after a rule was saved).</summary>
    public void Reset()
    {
        _rules = null;
        _bankAccounts = null;
        _accounts = null;
    }

    public async Task<ResolvedAccount> ResolveAsync(PostingScope scope, CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);

        if (string.Equals(scope.AccountRole, AccountRoles.Bank, StringComparison.OrdinalIgnoreCase) &&
            scope.BankAccountId.HasValue &&
            _bankAccounts!.TryGetValue(scope.BankAccountId.Value, out var linked) &&
            linked.HasValue)
        {
            return Usable(linked.Value, null, scope);
        }

        var rule = PostingRuleMatcher.Match(_rules!, scope)
                   ?? throw new BusinessRuleException(
                       $"لا توجد قاعدة ترحيل للدور «{scope.AccountRole}». أضفها من /api/v1/accounting/posting-rules ثم أعد محاولة الترحيل.");

        return Usable(rule.AccountId, rule.Id, scope);
    }

    private ResolvedAccount Usable(long accountId, long? ruleId, PostingScope scope)
    {
        if (!_accounts!.TryGetValue(accountId, out var account) || !account.Usable)
        {
            throw new BusinessRuleException(
                $"حساب قاعدة الترحيل للدور «{scope.AccountRole}» غير موجود أو غير مفعّل أو حساب رئيسي. صحّح القاعدة ثم أعد المحاولة.");
        }

        return new ResolvedAccount(accountId, account.Code, account.Name, ruleId);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_rules is not null)
        {
            return;
        }

        _rules = await _dbContext.AccountingPostingRules.AsNoTracking().Where(x => x.IsActive).ToListAsync(cancellationToken);
        _bankAccounts = await _dbContext.BankAccounts.AsNoTracking().IgnoreQueryFilters()
            .ToDictionaryAsync(x => x.Id, x => x.AccountId, cancellationToken);
        _accounts = await _dbContext.Accounts.AsNoTracking()
            .Select(x => new { x.Id, x.Code, x.ArabicName, Usable = x.IsActive && x.IsPostable })
            .ToDictionaryAsync(x => x.Id, x => (x.Code, x.ArabicName, x.Usable), cancellationToken);
    }
}
