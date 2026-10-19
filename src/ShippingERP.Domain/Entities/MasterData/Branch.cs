using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.MasterData;

/// <summary>
/// A branch (فرع) of the company. Most operational data (customers, employees, vehicles, warehouses,
/// shipments later) belongs to one branch, and users without Branches.ViewAll only see their own branch.
/// </summary>
public class Branch : SoftDeletableEntity
{
    /// <summary>Unique short code, e.g. "DAM". Upper case.</summary>
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    public long CityId { get; set; }

    public City City { get; set; } = null!;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    /// <summary>Only one branch can be the head office (المركز الرئيسي).</summary>
    public bool IsHeadOffice { get; set; }

    public string? Notes { get; set; }

    public ICollection<Warehouse> Warehouses { get; set; } = new List<Warehouse>();
}
