using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.MasterData;

/// <summary>A country. Code = ISO 3166 alpha-2 or alpha-3, e.g. "SY".</summary>
public class Country : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    /// <summary>International dialing code, e.g. "+963".</summary>
    public string? PhoneCode { get; set; }

    public ICollection<City> Cities { get; set; } = new List<City>();
}
