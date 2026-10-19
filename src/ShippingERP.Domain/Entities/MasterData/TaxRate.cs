using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.MasterData;

/// <summary>A tax rate, e.g. VAT 5%. One rate can be the default for new invoices.</summary>
public class TaxRate : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    /// <summary>Percentage from 0 to 100, decimal(5,2). Example: 5.00 = 5%.</summary>
    public decimal Percentage { get; set; }

    public bool IsDefault { get; set; }
}
