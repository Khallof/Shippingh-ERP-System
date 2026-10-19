using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.MasterData;

/// <summary>A warehouse (مستودع) that belongs to one branch. A branch can have several.</summary>
public class Warehouse : SoftDeletableEntity
{
    /// <summary>Unique code, e.g. "DAM-WH1".</summary>
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    /// <summary>Storage capacity in cubic meters (optional).</summary>
    public decimal? CapacityCubicMeters { get; set; }

    public string? Notes { get; set; }
}
