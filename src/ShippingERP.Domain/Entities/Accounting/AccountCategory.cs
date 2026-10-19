using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Accounting;

/// <summary>
/// Account category (تصنيف الحسابات): groups accounts in the financial statements ("النقدية وما في حكمها",
/// "الذمم المدينة", "المصاريف التشغيلية"...) and gives the cash flow section of their movements.
/// </summary>
public class AccountCategory : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    /// <summary>Accounts of the category must have this type.</summary>
    public AccountType AccountType { get; set; }

    public CashFlowActivity CashFlowActivity { get; set; } = CashFlowActivity.Operating;

    public int SortOrder { get; set; }

    /// <summary>Created by the chart of accounts seeder; cannot be deleted.</summary>
    public bool IsSystem { get; set; }

    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
