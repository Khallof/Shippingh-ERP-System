using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.MasterData;

/// <summary>Payment method (طريقة دفع): cash, COD, bank transfer...</summary>
public class PaymentMethod : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    public PaymentMethodKind Kind { get; set; }

    /// <summary>True when a reference number is mandatory (bank transfer number, card receipt...).</summary>
    public bool RequiresReference { get; set; }

    public int SortOrder { get; set; }
}
