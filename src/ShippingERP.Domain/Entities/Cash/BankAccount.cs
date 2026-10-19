using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Accounting;
using ShippingERP.Domain.Entities.MasterData;

namespace ShippingERP.Domain.Entities.Cash;

/// <summary>
/// A company bank account (حساب بنكي), used for cash deposits (cashbox -> bank transfers) and sender settlements
/// paid by bank transfer. Its balance belongs to the accounting module (bank reconciliation, next phase); here it
/// is only a reference with a currency.
///
/// Step 7: AccountId links it to its general ledger account (null = the account of the "Bank" posting rule).
/// Its balance is the sum of the posted journal lines that carry its id; the bank reconciliation compares that
/// balance with the bank statement.
/// </summary>
public class BankAccount : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string BankName { get; set; } = string.Empty;

    public string AccountNumber { get; set; } = string.Empty;

    public string? Iban { get; set; }

    public long CurrencyId { get; set; }

    public Currency Currency { get; set; } = null!;

    /// <summary>Optional: the branch that normally uses the account (null = all branches).</summary>
    public long? BranchId { get; set; }

    public Branch? Branch { get; set; }

    public string? Notes { get; set; }

    /// <summary>The general ledger account (step 7). Null = the account of the "Bank" posting rule.</summary>
    public long? AccountId { get; set; }

    public Account? Account { get; set; }
}
