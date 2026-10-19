using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Shipments;

// ------------------------------------------------------------------ requests

/// <summary>One package of a shipment. Volumetric weight is calculated from the dimensions (all three or none).</summary>
public sealed class ShipmentPackageRequest
{
    public string? Description { get; set; }

    /// <summary>Actual weight in kg (up to 3 decimals), greater than 0.</summary>
    public decimal WeightKg { get; set; }

    public decimal? LengthCm { get; set; }

    public decimal? WidthCm { get; set; }

    public decimal? HeightCm { get; set; }
}

/// <summary>
/// Body of POST /api/v1/shipments, PUT /api/v1/shipments/{id} and POST /api/v1/shipments/quote.
///
/// Empty optional fields are filled automatically:
/// - OriginCityId: the sender address city, otherwise the origin branch city.
/// - DestinationCityId / DestinationZoneId: from the recipient address.
/// - Addresses: the default address of each customer.
/// - DestinationBranchId: the only active branch of the destination city (if there is exactly one).
/// The shipping charge is calculated from the pricing rules; ManualShippingCharge and DiscountAmount
/// require the permission Shipments.OverridePrice and a ManualPriceReason. A change bigger than the approval
/// threshold (shipping settings) also requires Pricing.ApproveOverride; other users create the shipment at the
/// rule price and request the change with POST /shipments/{id}/apply-price.
/// </summary>
public sealed class SaveShipmentRequest
{
    public long SenderCustomerId { get; set; }

    public long RecipientCustomerId { get; set; }

    public long? SenderAddressId { get; set; }

    public long? RecipientAddressId { get; set; }

    /// <summary>The branch registering the shipment. Ignored on update (the origin never changes).</summary>
    public long OriginBranchId { get; set; }

    public long? DestinationBranchId { get; set; }

    public long? OriginCityId { get; set; }

    public long? DestinationCityId { get; set; }

    public long? DestinationZoneId { get; set; }

    public long ServiceTypeId { get; set; }

    /// <summary>What is inside, e.g. "ملابس", "أدوية", "قطع إلكترونية".</summary>
    public string PackageDescription { get; set; } = string.Empty;

    /// <summary>At least one package.</summary>
    public List<ShipmentPackageRequest> Packages { get; set; } = new();

    /// <summary>Step 9: price by weight (kg, default) or by volume (m³).</summary>
    public PricingBasis PricingBasis { get; set; } = PricingBasis.Weight;

    /// <summary>
    /// Step 9: total volume in m³ (3 decimals). Required (> 0) for volume pricing unless every package has its
    /// dimensions (then the volume is calculated from them).
    /// </summary>
    public decimal TotalVolumeCbm { get; set; }

    /// <summary>Step 9: who pays (is invoiced for) the shipping charge. Default Sender.</summary>
    public ChargesPayer ChargesPaidBy { get; set; } = ChargesPayer.Sender;

    /// <summary>
    /// Step 9: price per kg (PricingBasis = Weight) or per m³ (Volume). When set, the shipping charge is
    /// chargeable weight (or volume) x UnitPrice and no price rule is needed. Null = use the price rules.
    /// </summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>Step 9: currency of UnitPrice (e.g. USD per m³, LYD per kg). Null = the base currency.</summary>
    public long? CurrencyId { get; set; }

    public decimal DeclaredValue { get; set; }

    /// <summary>Cannot be more than DeclaredValue.</summary>
    public decimal InsuranceValue { get; set; }

    public bool IsCOD { get; set; }

    /// <summary>Required (> 0) when IsCOD is true; must be 0 otherwise.</summary>
    public decimal CODAmount { get; set; }

    /// <summary>A driver collects the shipment from the sender (pickup fee of the price rule).</summary>
    public bool RequiresPickup { get; set; }

    /// <summary>The company packs the shipment (packaging fee of the price rule).</summary>
    public bool RequiresPackaging { get; set; }

    /// <summary>
    /// Optional manual shipping subtotal (replaces base + weight + service fees; the COD commission and the tax are
    /// still added). Requires Shipments.OverridePrice and ManualPriceReason.
    /// </summary>
    public decimal? ManualShippingCharge { get; set; }

    /// <summary>Manual discount. Requires Shipments.OverridePrice and ManualPriceReason when greater than 0.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Required with a manual charge or a discount.</summary>
    public string? ManualPriceReason { get; set; }

    public string? SenderNotes { get; set; }

    public string? InternalNotes { get; set; }

    /// <summary>The sender's own order number (unique per sender).</summary>
    public string? ExternalReference { get; set; }
}

/// <summary>
/// Search: tracking number, shipment number, external reference, sender/recipient name and phone.
/// SortBy: createdAtUtc (default, newest first), shipmentNumber, trackingNumber, status, totalCharge,
/// codAmount, chargeableWeight, deliveredAtUtc.
/// </summary>
public sealed class ShipmentListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields =
    {
        "createdAtUtc", "shipmentNumber", "trackingNumber", "status", "totalCharge", "codAmount", "chargeableWeight", "deliveredAtUtc"
    };

    /// <summary>Exact tracking number.</summary>
    public string? TrackingNumber { get; set; }

    /// <summary>Exact shipment number (SHP-2026-000001).</summary>
    public string? ShipmentNumber { get; set; }

    /// <summary>Sender name contains / phone starts with this text.</summary>
    public string? Sender { get; set; }

    /// <summary>Recipient name contains / phone starts with this text.</summary>
    public string? Recipient { get; set; }

    public ShipmentStatus? Status { get; set; }

    /// <summary>Shipments whose origin, current or destination branch is this branch.</summary>
    public long? BranchId { get; set; }

    public long? OriginBranchId { get; set; }

    public long? CurrentBranchId { get; set; }

    public long? DestinationBranchId { get; set; }

    /// <summary>Assigned driver profile id.</summary>
    public long? DriverId { get; set; }

    public long? SenderCustomerId { get; set; }

    public long? RecipientCustomerId { get; set; }

    public long? OriginCityId { get; set; }

    public long? DestinationCityId { get; set; }

    public long? ServiceTypeId { get; set; }

    public bool? IsCOD { get; set; }

    /// <summary>Created on or after (UTC).</summary>
    public DateTime? CreatedFromUtc { get; set; }

    /// <summary>Created before (UTC, exclusive).</summary>
    public DateTime? CreatedToUtc { get; set; }
}

/// <summary>Body of PATCH /api/v1/shipments/{id}/status (scan-style changes only).</summary>
public sealed class ChangeShipmentStatusRequest
{
    /// <summary>PickedUp, ReceivedAtBranch or InWarehouse. Other statuses have their own workflow endpoint.</summary>
    public ShipmentStatus Status { get; set; }

    /// <summary>Required for InWarehouse: a warehouse of the shipment's current branch.</summary>
    public long? WarehouseId { get; set; }

    public string? Notes { get; set; }

    /// <summary>When it happened (default now). Cannot be in the future.</summary>
    public DateTime? OccurredAtUtc { get; set; }
}

public sealed class CancelShipmentRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Body of PATCH /api/v1/shipments/{id}/driver. DriverId = null removes the assignment.</summary>
public sealed class AssignDriverRequest
{
    public long? DriverId { get; set; }

    /// <summary>Default: the driver's default vehicle.</summary>
    public long? VehicleId { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Body of POST /api/v1/shipments/{id}/status-corrections. Sets any status (except Created) outside the normal
/// rules. Requires Shipments.CorrectStatus and a reason; recorded with IsCorrection = true.
/// </summary>
public sealed class CorrectShipmentStatusRequest
{
    public ShipmentStatus Status { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>Optional: also correct where the shipment physically is.</summary>
    public long? CurrentBranchId { get; set; }

    /// <summary>Required when the corrected status is InWarehouse.</summary>
    public long? WarehouseId { get; set; }
}

// ------------------------------------------------------------------ responses

public sealed class ShipmentPackageDto
{
    public long Id { get; init; }
    public int SequenceNumber { get; init; }
    public string PackageBarcode { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal WeightKg { get; init; }
    public decimal? LengthCm { get; init; }
    public decimal? WidthCm { get; init; }
    public decimal? HeightCm { get; init; }
    public decimal VolumetricWeightKg { get; init; }
}

/// <summary>One row of the shipment list.</summary>
public sealed class ShipmentListItemDto
{
    public long Id { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public string? ExternalReference { get; init; }
    public ShipmentStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public long SenderCustomerId { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string SenderPhone { get; init; } = string.Empty;
    public long RecipientCustomerId { get; init; }
    public string RecipientName { get; init; } = string.Empty;
    public string RecipientPhone { get; init; } = string.Empty;
    public long OriginBranchId { get; init; }
    public string OriginBranchName { get; init; } = string.Empty;
    public long CurrentBranchId { get; init; }
    public string CurrentBranchName { get; init; } = string.Empty;
    public long? DestinationBranchId { get; init; }
    public string? DestinationBranchName { get; init; }
    public long DestinationCityId { get; init; }
    public string DestinationCityName { get; init; } = string.Empty;
    public long ServiceTypeId { get; init; }
    public string ServiceTypeName { get; init; } = string.Empty;
    public long? AssignedDriverId { get; init; }
    public string? AssignedDriverName { get; init; }
    public int PackageCount { get; init; }
    public decimal ChargeableWeight { get; init; }
    public decimal TotalVolumeCbm { get; init; }
    public PricingBasis PricingBasis { get; init; }
    public ChargesPayer ChargesPaidBy { get; init; }
    public bool IsCOD { get; init; }
    public decimal CODAmount { get; init; }
    public decimal TotalCharge { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? DeliveredAtUtc { get; init; }
}

/// <summary>Full shipment details.</summary>
public sealed class ShipmentDto
{
    public long Id { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public string? ExternalReference { get; init; }
    public ShipmentStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;

    public long SenderCustomerId { get; init; }
    public string SenderCode { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string SenderPhone { get; init; } = string.Empty;
    public long? SenderAddressId { get; init; }
    public string? SenderAddressLine { get; init; }

    public long RecipientCustomerId { get; init; }
    public string RecipientCode { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;
    public string RecipientPhone { get; init; } = string.Empty;
    public long? RecipientAddressId { get; init; }
    public string? RecipientAddressLine { get; init; }
    public string? RecipientContactPhone { get; init; }

    public long OriginBranchId { get; init; }
    public string OriginBranchName { get; init; } = string.Empty;
    public long CurrentBranchId { get; init; }
    public string CurrentBranchName { get; init; } = string.Empty;
    public long? CurrentWarehouseId { get; init; }
    public string? CurrentWarehouseName { get; init; }
    public long? DestinationBranchId { get; init; }
    public string? DestinationBranchName { get; init; }
    public long OriginCityId { get; init; }
    public string OriginCityName { get; init; } = string.Empty;
    public long DestinationCityId { get; init; }
    public string DestinationCityName { get; init; } = string.Empty;
    public long? OriginZoneId { get; init; }
    public string? OriginZoneName { get; init; }
    public long? DestinationZoneId { get; init; }
    public string? DestinationZoneName { get; init; }
    public long ServiceTypeId { get; init; }
    public string ServiceTypeName { get; init; } = string.Empty;

    public long? AssignedDriverId { get; init; }
    public string? AssignedDriverName { get; init; }
    public long? VehicleId { get; init; }
    public string? VehiclePlateNumber { get; init; }
    public int DeliveryAttemptCount { get; init; }
    public long? ReturnBranchId { get; init; }

    public int PackageCount { get; init; }
    public decimal ActualWeight { get; init; }
    public decimal VolumetricWeight { get; init; }
    public decimal ChargeableWeight { get; init; }
    public decimal TotalVolumeCbm { get; init; }
    public PricingBasis PricingBasis { get; init; }
    public ChargesPayer ChargesPaidBy { get; init; }
    public decimal? UnitPrice { get; init; }
    public string PackageDescription { get; init; } = string.Empty;

    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal DeclaredValue { get; init; }
    public decimal InsuranceValue { get; init; }
    public bool IsCOD { get; init; }
    public decimal CODAmount { get; init; }
    public bool RequiresPickup { get; init; }
    public bool RequiresPackaging { get; init; }
    public decimal ShippingCharge { get; init; }
    /// <summary>Total discount (contract + manual).</summary>
    public decimal DiscountAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalCharge { get; init; }

    /// <summary>Current manual subtotal (null = rule price). Send it back unchanged in PUT to keep it.</summary>
    public decimal? ManualShippingCharge { get; init; }
    /// <summary>Current manual discount (send it back as DiscountAmount in PUT to keep it).</summary>
    public decimal ManualDiscountAmount { get; init; }

    /// <summary>Number of the active invoice of the shipping charge (draft invoices have no number yet).</summary>
    public long? InvoiceId { get; init; }
    public string? InvoiceNumber { get; init; }
    /// <summary>True while a manual price change waits for approval.</summary>
    public bool HasPendingPriceOverride { get; init; }

    /// <summary>Step 6: the COD record of the shipment (null when it was never a COD shipment).</summary>
    public long? CodTransactionId { get; init; }
    public CODStatus? CodStatus { get; init; }
    public decimal? CodCollectedAmount { get; init; }

    public string? SenderNotes { get; init; }
    public string? InternalNotes { get; init; }
    public string? CancellationReason { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedByUserId { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public long? UpdatedByUserId { get; init; }
    public DateTime? PickedUpAtUtc { get; init; }
    public DateTime? DeliveredAtUtc { get; init; }
    public DateTime? ReturnedAtUtc { get; init; }
    public DateTime? CancelledAtUtc { get; init; }

    public IReadOnlyList<ShipmentPackageDto> Packages { get; init; } = Array.Empty<ShipmentPackageDto>();

    /// <summary>Statuses reachable from the current one by the normal workflows.</summary>
    public IReadOnlyList<ShipmentStatus> NextStatuses { get; init; } = Array.Empty<ShipmentStatus>();
}

public sealed class ShipmentStatusHistoryDto
{
    public long Id { get; init; }
    public ShipmentStatus? FromStatus { get; init; }
    public string? FromStatusName { get; init; }
    public ShipmentStatus ToStatus { get; init; }
    public string ToStatusName { get; init; } = string.Empty;
    public ShipmentStatusChangeSource Source { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public long? BranchId { get; init; }
    public string? BranchName { get; init; }
    public long? WarehouseId { get; init; }
    public long? DriverId { get; init; }
    public string? Notes { get; init; }
    public string? ReferenceType { get; init; }
    public long? ReferenceId { get; init; }
    public string? ReferenceNumber { get; init; }
    public bool IsCorrection { get; init; }
    public long? ChangedByUserId { get; init; }
    public string? ChangedByUserName { get; init; }
    public DateTime RecordedAtUtc { get; init; }
}

/// <summary>An applied weight tier.</summary>
public sealed class PricingWeightTierDto
{
    public decimal FromKg { get; init; }
    public decimal? ToKg { get; init; }
    public decimal Kg { get; init; }
    public decimal PricePerKg { get; init; }
    public decimal Charge { get; init; }
}

/// <summary>
/// A price breakdown: a pricing snapshot, or the result of POST /shipments/quote and
/// POST /shipments/{id}/calculate-price (then Id is null).
///
///   ShippingSubtotal    = BaseCharge + WeightCharge + ServiceFeesTotal   (or the manual charge)
///   TotalShippingCharge = ShippingSubtotal - DiscountAmount
///   TaxableAmount       = TotalShippingCharge + CodCommission
///   NetTotal            = TaxableAmount + TaxAmount                      (= the shipment TotalCharge)
/// </summary>
public sealed class ShipmentPricingDto
{
    public long? Id { get; init; }
    public string Reason { get; init; } = string.Empty;
    public PricingSource PricingSource { get; init; }
    public long? PricingPlanId { get; init; }
    public string? PricingPlanCode { get; init; }
    public long? PricingRuleId { get; init; }
    public string? PricingRuleName { get; init; }
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;

    // Inputs
    public decimal ActualWeight { get; init; }
    public decimal VolumetricWeight { get; init; }
    public decimal ChargeableWeight { get; init; }
    public int VolumetricDivisor { get; init; }
    public decimal WeightRoundingStepKg { get; init; }
    public int PackageCount { get; init; }
    public PricingBasis PricingBasis { get; init; }
    public decimal VolumeCbm { get; init; }
    public decimal PricePerCubicMeter { get; init; }

    /// <summary>VolumeCbm x PricePerCubicMeter (shipments priced by volume).</summary>
    public decimal VolumeCharge { get; init; }
    public decimal InsuranceValue { get; init; }
    public bool RequiresPickup { get; init; }
    public bool RequiresPackaging { get; init; }
    public bool IsRemoteArea { get; init; }
    public bool IsReturn { get; init; }

    // Base and weight
    public decimal BaseCharge { get; init; }
    public decimal IncludedWeightKg { get; init; }
    public decimal AdditionalWeightKg { get; init; }
    public decimal WeightCharge { get; init; }
    public IReadOnlyList<PricingWeightTierDto> WeightTiers { get; init; } = Array.Empty<PricingWeightTierDto>();

    // Service fees
    public decimal PackageFee { get; init; }
    public decimal InsuranceFee { get; init; }
    public decimal PickupFee { get; init; }
    public decimal PackagingFee { get; init; }
    public decimal RemoteAreaFee { get; init; }
    public decimal ReturnFee { get; init; }
    public decimal ServiceFeesTotal { get; init; }

    // Subtotal and discount
    public decimal ShippingSubtotal { get; init; }
    /// <summary>The rule subtotal that a manual charge replaced (null otherwise).</summary>
    public decimal? CalculatedShippingSubtotal { get; init; }
    public decimal ContractDiscountPercentage { get; init; }
    public decimal ContractDiscountAmount { get; init; }
    public decimal ManualDiscountAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TotalShippingCharge { get; init; }

    // COD commission
    public decimal CodAmount { get; init; }
    public decimal CodFeeFixed { get; init; }
    public decimal CodFeePercentage { get; init; }
    public decimal CodFeeMinimum { get; init; }
    public decimal CodCommission { get; init; }

    // Tax and totals
    public decimal TaxableAmount { get; init; }
    public long? TaxRateId { get; init; }
    public decimal TaxPercentage { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal NetTotal { get; init; }

    /// <summary>Gross charge stored on the shipment (ShippingSubtotal + CodCommission).</summary>
    public decimal ShippingCharge { get; init; }

    /// <summary>Same as NetTotal (kept for step-4 clients).</summary>
    public decimal TotalCharge { get; init; }

    public string? OverrideReason { get; init; }
    public long? PriceOverrideId { get; init; }
    public DateTime? CalculatedAtUtc { get; init; }
}

/// <summary>Tracking view: GET /api/v1/shipments/track/{trackingNumber}.</summary>
public sealed class ShipmentTrackingDto
{
    public long ShipmentId { get; init; }
    public string TrackingNumber { get; init; } = string.Empty;
    public string ShipmentNumber { get; init; } = string.Empty;
    public ShipmentStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public string OriginCityName { get; init; } = string.Empty;
    public string DestinationCityName { get; init; } = string.Empty;
    public string CurrentBranchName { get; init; } = string.Empty;
    public int PackageCount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? DeliveredAtUtc { get; init; }
    public IReadOnlyList<ShipmentTrackingEventDto> Events { get; init; } = Array.Empty<ShipmentTrackingEventDto>();
}

public sealed class ShipmentTrackingEventDto
{
    public DateTime OccurredAtUtc { get; init; }
    public ShipmentStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public string? BranchName { get; init; }
    public string? Notes { get; init; }
}
