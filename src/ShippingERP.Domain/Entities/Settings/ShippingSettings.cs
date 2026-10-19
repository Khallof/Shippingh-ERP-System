using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.Settings;

/// <summary>
/// Company-wide shipping settings (one row). When the row does not exist yet the defaults below are used.
/// Changed with PUT /api/v1/shipping-settings. Existing shipments keep their values (pricing snapshots).
/// </summary>
public class ShippingSettings : BaseEntity
{
    public const int DefaultVolumetricDivisor = 5000;
    public const decimal DefaultWeightRoundingStepKg = 0.5m;
    public const string DefaultTrackingNumberPrefix = "SE";
    public const int DefaultLabelWidthMm = 100;
    public const int DefaultLabelHeightMm = 150;
    public const decimal DefaultPriceOverrideApprovalThresholdPercent = 20m;
    public const int StandardPaymentTermsDays = 30;

    /// <summary>Volumetric weight = L x W x H (cm) / divisor. Common values: 4000, 5000, 6000.</summary>
    public int VolumetricDivisor { get; set; } = DefaultVolumetricDivisor;

    /// <summary>Chargeable weight is rounded UP to this step (0 = no rounding).</summary>
    public decimal WeightRoundingStepKg { get; set; } = DefaultWeightRoundingStepKg;

    /// <summary>2-4 upper-case letters in front of every tracking number, e.g. "SE".</summary>
    public string TrackingNumberPrefix { get; set; } = DefaultTrackingNumberPrefix;

    /// <summary>Thermal label size. 100 x 150 mm (4 x 6 inch) is the most common courier size.</summary>
    public int LabelWidthMm { get; set; } = DefaultLabelWidthMm;

    public int LabelHeightMm { get; set; } = DefaultLabelHeightMm;

    /// <summary>
    /// Optional public tracking page, e.g. "https://track.example.com/{trackingNumber}". When set, the QR code on
    /// the label opens this URL; otherwise the QR code contains the tracking number only.
    /// </summary>
    public string? TrackingUrlTemplate { get; set; }

    // ---------------- Step 5: pricing and invoicing ----------------

    /// <summary>
    /// A manual price / discount that changes the rule price by MORE than this percentage needs the approval of a
    /// user with Pricing.ApproveOverride (0 = every manual change needs approval).
    /// </summary>
    public decimal PriceOverrideApprovalThresholdPercent { get; set; } = DefaultPriceOverrideApprovalThresholdPercent;

    /// <summary>Payment terms of new invoices when the request does not give any (0 = due on issue).</summary>
    public int DefaultPaymentTermsDays { get; set; } = StandardPaymentTermsDays;
}
