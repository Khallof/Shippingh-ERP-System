using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.MasterData;

/// <summary>Expense category (بند مصروف): fuel, maintenance, rent... Linked to accounts in the accounting step.</summary>
public class ExpenseCategory : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    public string? Description { get; set; }
}
