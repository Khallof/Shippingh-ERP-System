using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.MasterData;

/// <summary>
/// A delivery zone / district (منطقة) inside a city. Used for pricing (origin zone -> destination zone)
/// and later for driver routes. Code is unique within its city.
/// </summary>
public class Zone : SoftDeletableEntity
{
    public long CityId { get; set; }

    public City City { get; set; } = null!;

    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    /// <summary>Hard-to-reach area: shipments delivered here pay the price rule's remote area fee (step 5).</summary>
    public bool IsRemoteArea { get; set; }

    public string? Notes { get; set; }
}
