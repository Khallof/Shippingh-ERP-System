using ShippingERP.Application.DTOs.Common;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Pricing;

/// <summary>
/// Body of POST /api/v1/shipments/{id}/calculate-price (preview, nothing is saved) and
/// POST /api/v1/shipments/{id}/apply-price (saves a new pricing snapshot).
///
/// Empty body = recalculate from the current pricing rules (e.g. after the rules changed or a return fee applies).
/// ManualShippingCharge / ManualDiscountAmount = manual price change: requires Shipments.OverridePrice and a Reason.
/// A change bigger than the approval threshold also needs Pricing.ApproveOverride, otherwise apply-price records
/// a Pending request (202) that an approver approves or rejects.
/// </summary>
public sealed class ShipmentPriceRequest
{
    /// <summary>Manual shipping subtotal (replaces base + weight + service fees).</summary>
    public decimal? ManualShippingCharge { get; set; }

    public decimal ManualDiscountAmount { get; set; }

    /// <summary>Required with a manual charge or a discount.</summary>
    public string? Reason { get; set; }
}

/// <summary>Result of calculate-price / apply-price.</summary>
public sealed class ShipmentPriceResultDto
{
    public long ShipmentId { get; init; }

    /// <summary>The breakdown (applied, proposed or pending).</summary>
    public ShipmentPricingDto Pricing { get; init; } = null!;

    /// <summary>The breakdown of the current pricing rules without manual values (null when no rule matches).</summary>
    public ShipmentPricingDto? RulePricing { get; init; }

    public bool IsManual { get; init; }

    /// <summary>|manual - rule| / rule x 100 on the total shipping charge (null without a manual change or a rule).</summary>
    public decimal? DeviationPercentage { get; init; }

    public decimal ApprovalThresholdPercent { get; init; }

    /// <summary>True when the manual change needs an approver.</summary>
    public bool RequiresApproval { get; init; }

    /// <summary>apply-price only: true when the new price is now the shipment price.</summary>
    public bool Applied { get; init; }

    /// <summary>apply-price only: the recorded manual change (Pending when it waits for approval).</summary>
    public PriceOverrideDto? Override { get; init; }
}

public sealed class PriceOverrideDto
{
    public long Id { get; init; }
    public long ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public PriceOverrideStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public decimal? ManualShippingCharge { get; init; }
    public decimal ManualDiscountAmount { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal? CalculatedTotalShippingCharge { get; init; }
    public decimal ProposedTotalShippingCharge { get; init; }
    public decimal? DeviationPercentage { get; init; }
    public bool RequiresApproval { get; init; }
    public long? RequestedByUserId { get; init; }
    public string? RequestedByUserName { get; init; }
    public DateTime RequestedAtUtc { get; init; }
    public long? DecidedByUserId { get; init; }
    public string? DecidedByUserName { get; init; }
    public DateTime? DecidedAtUtc { get; init; }
    public string? DecisionNotes { get; init; }
    public long? AppliedPricingSnapshotId { get; init; }
}

/// <summary>
/// GET /api/v1/price-overrides. Search: shipment / tracking number, sender name.
/// SortBy: createdAtUtc (default, newest first), deviationPercentage, status.
/// </summary>
public sealed class PriceOverrideListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "createdAtUtc", "deviationPercentage", "status" };

    public PriceOverrideStatus? Status { get; set; }

    public long? ShipmentId { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }
}

/// <summary>Body of POST /api/v1/price-overrides/{id}/approve and /reject (notes are required to reject).</summary>
public sealed class DecidePriceOverrideRequest
{
    public string? Notes { get; set; }
}
