using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Accounting;

/// <summary>Sign conventions of the account types (pure functions, unit tested).</summary>
public static class AccountRules
{
    /// <summary>Assets, cost of revenue and expenses grow with debits; liabilities, equity and revenue with credits.</summary>
    public static NormalBalance NormalBalanceOf(AccountType type) =>
        type is AccountType.Assets or AccountType.CostOfRevenue or AccountType.Expenses ? NormalBalance.Debit : NormalBalance.Credit;

    /// <summary>Assets, liabilities and equity: balance sheet. The others: profit and loss.</summary>
    public static bool IsBalanceSheet(AccountType type) =>
        type is AccountType.Assets or AccountType.Liabilities or AccountType.Equity;

    public static bool IsProfitAndLoss(AccountType type) => !IsBalanceSheet(type);

    /// <summary>The balance in the natural direction of the type (positive = normal balance).</summary>
    public static decimal NaturalBalance(AccountType type, decimal debit, decimal credit) =>
        NormalBalanceOf(type) == NormalBalance.Debit ? debit - credit : credit - debit;

    /// <summary>A net amount (debit − credit) shown in the debit or the credit column.</summary>
    public static (decimal Debit, decimal Credit) ToColumns(decimal net) => net >= 0m ? (net, 0m) : (0m, -net);

    /// <summary>The profit-and-loss effect of a line: revenue minus costs (credit − debit).</summary>
    public static decimal ProfitEffect(decimal debit, decimal credit) => credit - debit;
}
