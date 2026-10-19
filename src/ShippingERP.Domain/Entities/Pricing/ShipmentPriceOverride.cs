using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Pricing;

/// <summary>
/// A manual price change of a shipment (تعديل سعر يدوي): a manual shipping charge and/or a manual discount.
///
/// Every manual change through POST /shipments/{id}/apply-price is recorded here with its reason.
/// - Small changes (deviation &lt;= ShippingSettings.PriceOverrideApprovalThresholdPercent) and changes made by a
///   user holding Pricing.ApproveOverride are applied at once (Status = Approved, DecidedByUserId = the user).
/// - Bigger changes by other users stay Pending until an approver approves (applied then) or rejects them.
/// CreatedByUserId is the requester. The approver must be a different user.
/// </summary>
public class ShipmentPriceOverride : BaseEntity
{
    public long ShipmentId { get; set; }

    public Shipment Shipment { get; set; } = null!;

    public PriceOverrideStatus Status { get; set; } = PriceOverrideStatus.Pending;

    /// <summary>Requested shipping subtotal (before discount, COD commission and tax). Null = keep the rule price.</summary>
    public decimal? ManualShippingCharge { get; set; }

    /// <summary>Requested manual discount (on top of any contract discount).</summary>
    public decimal ManualDiscountAmount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public long CurrencyId { get; set; }

    /// <summary>Total shipping charge (after discount, before COD commission and tax) by the rules. Null = no rule matched.</summary>
    public decimal? CalculatedTotalShippingCharge { get; set; }

    /// <summary>Total shipping charge (after discount, before COD commission and tax) with the manual values.</summary>
    public decimal ProposedTotalShippingCharge { get; set; }

    /// <summary>|proposed - calculated| / calculated x 100. Null when no rule matched (always needs approval).</summary>
    public decimal? DeviationPercentage { get; set; }

    public bool RequiresApproval { get; set; }

    public long? DecidedByUserId { get; set; }

    public DateTime? DecidedAtUtc { get; set; }

    public string? DecisionNotes { get; set; }

    /// <summary>The pricing snapshot created when the override was applied.</summary>
    public long? AppliedPricingSnapshotId { get; set; }
}
