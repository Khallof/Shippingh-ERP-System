using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.MasterData;

/// <summary>A city inside a country. Code is unique within its country.</summary>
public class City : SoftDeletableEntity
{
    public long CountryId { get; set; }

    public Country Country { get; set; } = null!;

    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    public ICollection<Zone> Zones { get; set; } = new List<Zone>();
}
