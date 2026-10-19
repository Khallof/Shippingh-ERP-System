using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Pricing;

/// <summary>
/// One condition of a price rule (شرط تسعير).
///
/// - Reference types (OriginZone, DestinationZone, OriginCity, DestinationCity, ServiceType): ReferenceId is set,
///   MinValue / MaxValue are null.
/// - Range types (ChargeableWeight, PackageCount): MinValue (inclusive) and/or MaxValue (exclusive) are set,
///   ReferenceId is null. Example: weight [5, 10) = 5 kg up to (not including) 10 kg.
///
/// Conditions of the same type are OR-ed, different types are AND-ed (see PriceConditionType).
/// The rows are replaced as a whole when the rule is saved; a shipment keeps what was applied in its snapshot.
/// </summary>
public class PriceRuleCondition : BaseEntity
{
    public long PriceRuleId { get; set; }

    public PriceRule PriceRule { get; set; } = null!;

    public PriceConditionType ConditionType { get; set; }

    /// <summary>Zone, city or service type id (reference types only). No foreign key: the type decides the table.</summary>
    public long? ReferenceId { get; set; }

    /// <summary>Inclusive lower limit (range types only), decimal(10,3).</summary>
    public decimal? MinValue { get; set; }

    /// <summary>Exclusive upper limit (range types only), decimal(10,3). Null = no upper limit.</summary>
    public decimal? MaxValue { get; set; }
}
