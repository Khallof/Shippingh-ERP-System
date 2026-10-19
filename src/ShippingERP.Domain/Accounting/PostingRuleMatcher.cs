using ShippingERP.Domain.Entities.Accounting;

namespace ShippingERP.Domain.Accounting;

/// <summary>What a posting line needs an account for: its role and its scopes.</summary>
public sealed record PostingScope(
    string AccountRole,
    long? BranchId = null,
    long? CurrencyId = null,
    long? CashboxId = null,
    long? BankAccountId = null,
    long? ExpenseCategoryId = null);

/// <summary>
/// Chooses the posting rule of a line (pure function, unit tested). A rule matches when its role is the line role
/// and each of its scopes is empty or equal to the line value. The most specific matching rule wins (most
/// non-empty scopes), then the highest Priority, then the oldest rule.
/// </summary>
public static class PostingRuleMatcher
{
    public static AccountingPostingRule? Match(IEnumerable<AccountingPostingRule> rules, PostingScope scope) =>
        rules
            .Where(rule => rule.IsActive && !rule.IsDeleted && Matches(rule, scope))
            .OrderByDescending(Specificity)
            .ThenByDescending(rule => rule.Priority)
            .ThenBy(rule => rule.Id)
            .FirstOrDefault();

    public static bool Matches(AccountingPostingRule rule, PostingScope scope) =>
        string.Equals(rule.AccountRole, scope.AccountRole, StringComparison.OrdinalIgnoreCase) &&
        (rule.BranchId is null || rule.BranchId == scope.BranchId) &&
        (rule.CurrencyId is null || rule.CurrencyId == scope.CurrencyId) &&
        (rule.CashboxId is null || rule.CashboxId == scope.CashboxId) &&
        (rule.BankAccountId is null || rule.BankAccountId == scope.BankAccountId) &&
        (rule.ExpenseCategoryId is null || rule.ExpenseCategoryId == scope.ExpenseCategoryId);

    /// <summary>Number of non-empty scopes.</summary>
    public static int Specificity(AccountingPostingRule rule) =>
        (rule.BranchId.HasValue ? 1 : 0) +
        (rule.CurrencyId.HasValue ? 1 : 0) +
        (rule.CashboxId.HasValue ? 1 : 0) +
        (rule.BankAccountId.HasValue ? 1 : 0) +
        (rule.ExpenseCategoryId.HasValue ? 1 : 0);
}
