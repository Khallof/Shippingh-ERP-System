using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Accounting;

/// <summary>
/// A general ledger account (حساب في دليل الحسابات). The chart is a tree: header accounts (IsPostable = false)
/// only total their children; journal lines can only use postable (leaf) accounts.
///
/// Balances are never stored: they are the sum of the posted journal lines (one source of truth, nothing to
/// get out of sync).
/// </summary>
public class Account : SoftDeletableEntity
{
    /// <summary>Unique account number, e.g. "1110". Children usually start with the parent code.</summary>
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    public AccountType AccountType { get; set; }

    /// <summary>Debit for assets / cost of revenue / expenses, credit for liabilities / equity / revenue.</summary>
    public NormalBalance NormalBalance { get; set; }

    public long? AccountCategoryId { get; set; }

    public AccountCategory? AccountCategory { get; set; }

    public long? ParentAccountId { get; set; }

    public Account? ParentAccount { get; set; }

    public ICollection<Account> Children { get; set; } = new List<Account>();

    /// <summary>1 = top level.</summary>
    public int Level { get; set; } = 1;

    /// <summary>False = header (group) account: no journal line may use it.</summary>
    public bool IsPostable { get; set; } = true;

    /// <summary>Created by the seeder; its type cannot change and it cannot be deleted.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Cash or cash equivalent (cashboxes, bank, cash in transit): the cash flow report is built on these.</summary>
    public bool IsCashAccount { get; set; }

    /// <summary>
    /// False for control accounts kept by a sub-ledger (cashboxes, customers, driver custody, sender COD money):
    /// only the automatic postings may use them, so the sub-ledger and the general ledger stay equal.
    /// </summary>
    public bool AllowManualPosting { get; set; } = true;

    /// <summary>Every line on this account must carry a cost center.</summary>
    public bool RequiresCostCenter { get; set; }

    /// <summary>Optional: only entries in this currency may use the account (e.g. a USD bank account).</summary>
    public long? CurrencyId { get; set; }

    public Currency? Currency { get; set; }

    public string? Description { get; set; }
}
