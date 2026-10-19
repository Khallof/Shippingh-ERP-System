using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.Customers;

/// <summary>
/// One address of a customer (home, office, warehouse...). Each address has its own contact name and
/// phone numbers, so a customer can have many phones. Exactly one address is the default.
/// </summary>
public class CustomerAddress : SoftDeletableEntity
{
    public long CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    /// <summary>Short label, e.g. "المنزل", "المكتب".</summary>
    public string Label { get; set; } = string.Empty;

    public long CityId { get; set; }

    public MasterData.City City { get; set; } = null!;

    public long? ZoneId { get; set; }

    public MasterData.Zone? Zone { get; set; }

    public string AddressLine { get; set; } = string.Empty;

    /// <summary>أقرب معلم</summary>
    public string? Landmark { get; set; }

    public string? BuildingNumber { get; set; }

    public string? PostalCode { get; set; }

    public string? ContactName { get; set; }

    public string? Phone { get; set; }

    public string? AlternatePhone { get; set; }

    /// <summary>decimal(9,6).</summary>
    public decimal? Latitude { get; set; }

    /// <summary>decimal(9,6).</summary>
    public decimal? Longitude { get; set; }

    public bool IsDefault { get; set; }
}
