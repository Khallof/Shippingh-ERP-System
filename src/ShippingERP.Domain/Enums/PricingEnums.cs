namespace ShippingERP.Domain.Enums;

// Enums of the pricing engine (step 5). Stored as text, so never rename a value.

/// <summary>
/// What a <see cref="Entities.Pricing.PriceRuleCondition"/> tests.
///
/// Reference conditions (zones, cities, service type) compare an id: ReferenceId must equal the shipment value.
/// Range conditions (weight, package count) compare a number: MinValue (inclusive) &lt;= value &lt; MaxValue (exclusive).
///
/// Several conditions of the SAME type are alternatives (OR): "destination zone = Mazzeh OR Malki".
/// Conditions of DIFFERENT types must all match (AND). A rule without conditions matches every shipment.
/// </summary>
public enum PriceConditionType
{
    OriginZone = 1,
    DestinationZone = 2,
    OriginCity = 3,
    DestinationCity = 4,
    ServiceType = 5,

    /// <summary>Chargeable weight in kg (max of actual and volumetric, rounded).</summary>
    ChargeableWeight = 6,

    /// <summary>Number of packages in the shipment.</summary>
    PackageCount = 7
}

/// <summary>Life cycle of a manual price change that needs approval.</summary>
public enum PriceOverrideStatus
{
    /// <summary>Waiting for a user with Pricing.ApproveOverride.</summary>
    Pending = 1,

    /// <summary>Approved and applied to the shipment (also used for overrides applied directly by an approver).</summary>
    Approved = 2,

    Rejected = 3,

    /// <summary>Replaced by a newer request, or the shipment can no longer be repriced.</summary>
    Cancelled = 4
}

public static class PriceConditionTypes
{
    /// <summary>True for conditions that compare an id (zones, cities, service type).</summary>
    public static bool IsReference(PriceConditionType type) =>
        type is PriceConditionType.OriginZone
            or PriceConditionType.DestinationZone
            or PriceConditionType.OriginCity
            or PriceConditionType.DestinationCity
            or PriceConditionType.ServiceType;

    /// <summary>True for conditions that compare a number with a [min, max) range.</summary>
    public static bool IsRange(PriceConditionType type) =>
        type is PriceConditionType.ChargeableWeight or PriceConditionType.PackageCount;

    public static string Arabic(PriceConditionType type) => type switch
    {
        PriceConditionType.OriginZone => "منطقة الانطلاق",
        PriceConditionType.DestinationZone => "منطقة الوصول",
        PriceConditionType.OriginCity => "مدينة الانطلاق",
        PriceConditionType.DestinationCity => "مدينة الوصول",
        PriceConditionType.ServiceType => "نوع الخدمة",
        PriceConditionType.ChargeableWeight => "الوزن المحتسب",
        PriceConditionType.PackageCount => "عدد الطرود",
        _ => type.ToString()
    };
}
