using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.MasterData;

/// <summary>Delivery service level, e.g. Standard, Express, Same day.</summary>
public class ServiceType : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    public string? Description { get; set; }

    /// <summary>Promised delivery time in days (0 = same day).</summary>
    public int EstimatedDeliveryDays { get; set; }

    /// <summary>Display order in drop-down lists.</summary>
    public int SortOrder { get; set; }
}
