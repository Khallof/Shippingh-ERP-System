using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.MasterData;

/// <summary>A supplier (مورد): fuel station, garage, office supplies, partner carrier...</summary>
public class Supplier : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    public string? ContactPerson { get; set; }

    public string Phone { get; set; } = string.Empty;

    public string? AlternatePhone { get; set; }

    public string? Email { get; set; }

    public string? TaxNumber { get; set; }

    public string? Address { get; set; }

    /// <summary>Balance owed to the supplier when the system started (positive = we owe them).</summary>
    public decimal OpeningBalance { get; set; }

    public string? Notes { get; set; }
}
