using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Identity;
using ShippingERP.Domain.Entities.MasterData;

namespace ShippingERP.Domain.Entities.Cash;

/// <summary>
/// A branch cashbox (صندوق نقدية), code e.g. "DAM-SYP-01". A branch can have several cashboxes, each in ONE
/// currency. CurrentBalance = sum(AmountIn - AmountOut) of its CashboxTransactions; it is kept here for fast
/// reads and protected by RowVersion, so two operations on the same cashbox at the same moment cannot both
/// succeed (the second gets 409). The balance can never become negative.
///
/// Branch and currency cannot change after creation. A cashbox is never deleted; it can be deactivated when its
/// balance is zero.
/// </summary>
public class Cashbox : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public long CurrencyId { get; set; }

    public Currency Currency { get; set; } = null!;

    /// <summary>The cashier responsible for the cash (أمين الصندوق). Optional.</summary>
    public long? CustodianUserId { get; set; }

    public ApplicationUser? CustodianUser { get; set; }

    public decimal CurrentBalance { get; set; }

    public DateTime? LastTransactionAtUtc { get; set; }

    public string? Notes { get; set; }

    public ICollection<CashboxTransaction> Transactions { get; set; } = new List<CashboxTransaction>();
}
