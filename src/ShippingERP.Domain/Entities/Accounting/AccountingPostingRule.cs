using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.Accounting;

/// <summary>
/// Automatic posting rule (قاعدة الترحيل الآلي): which general ledger account receives the lines of an account
/// ROLE ("CashOnHand", "ShippingRevenue", "Expense"...) of the operational events.
///
/// The optional scopes make a rule more specific: a rule for role Expense + expense category FUEL beats the general
/// Expense rule; a rule for role CashOnHand + cashbox DAM-SYP-01 beats the CashOnHand rule. The most specific
/// matching rule wins, then the highest Priority (PostingRuleMatcher).
/// </summary>
public class AccountingPostingRule : SoftDeletableEntity
{
    /// <summary>One of Application/IntegrationEvents/AccountRoles.</summary>
    public string AccountRole { get; set; } = string.Empty;

    public long AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public long? BranchId { get; set; }

    public long? CurrencyId { get; set; }

    public long? CashboxId { get; set; }

    public long? BankAccountId { get; set; }

    public long? ExpenseCategoryId { get; set; }

    public int Priority { get; set; }

    public string? Description { get; set; }
}
