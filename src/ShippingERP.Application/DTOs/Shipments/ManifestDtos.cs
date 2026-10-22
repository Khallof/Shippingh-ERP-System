using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Shipments;

/// <summary>
/// Shipments to add to a manifest / transfer, by tracking number or shipment number (barcode scanner input).
/// Up to 200 codes per call.
/// </summary>
public sealed class ShipmentCodesRequest
{
    public List<string> Codes { get; set; } = new();
}

/// <summary>Base query for document lists (manifests, transfers, returns, damage cases).</summary>
public abstract class DocumentListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "createdAtUtc", "number" };

    /// <summary>Created on or after (UTC).</summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>Created before (UTC, exclusive).</summary>
    public DateTime? ToUtc { get; set; }
}

/// <summary>A shipment as shown on a manifest line.</summary>
public class ManifestShipmentDto
{
    public long ItemId { get; init; }
    public long ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public ShipmentStatus ShipmentStatus { get; init; }
    public string ShipmentStatusName { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;
    public string RecipientPhone { get; init; } = string.Empty;
    public string DestinationCityName { get; init; } = string.Empty;
    public string? DestinationZoneName { get; init; }
    public int PackageCount { get; init; }
    public decimal ChargeableWeight { get; init; }
    public bool IsCOD { get; init; }
    public decimal CODAmount { get; init; }
    public decimal TotalVolumeCbm { get; init; }
    public PricingBasis PricingBasis { get; init; }
    public ChargesPayer ChargesPaidBy { get; init; }
    public string? ConsignmentNumber { get; init; }
}

// ------------------------------------------------------------------ warehouse movement manifests

public sealed class CreateWarehouseManifestRequest
{
    public WarehouseMovementType MovementType { get; set; } = WarehouseMovementType.CheckIn;

    /// <summary>The warehouse (its branch becomes the manifest branch).</summary>
    public long WarehouseId { get; set; }

    public string? Notes { get; set; }

    /// <summary>Optional shipments to add immediately.</summary>
    public List<string> Codes { get; set; } = new();
}

public sealed class WarehouseManifestDto
{
    public long Id { get; init; }
    public string ManifestNumber { get; init; } = string.Empty;
    public WarehouseMovementType MovementType { get; init; }
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public long WarehouseId { get; init; }
    public string WarehouseName { get; init; } = string.Empty;
    public WarehouseManifestStatus Status { get; init; }
    public int ItemCount { get; init; }
    public DateTime? ConfirmedAtUtc { get; init; }
    public long? ConfirmedByUserId { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedByUserId { get; init; }
    /// <summary>Only in GET by id.</summary>
    public IReadOnlyList<ManifestShipmentDto>? Items { get; init; }
}

/// <summary>SortBy: createdAtUtc (default, newest first), number.</summary>
public sealed class WarehouseManifestListQuery : DocumentListQuery
{
    public long? BranchId { get; set; }
    public long? WarehouseId { get; set; }
    public WarehouseManifestStatus? Status { get; set; }
    public WarehouseMovementType? MovementType { get; set; }
}

// ------------------------------------------------------------------ branch transfers

public sealed class CreateBranchTransferRequest
{
    public long FromBranchId { get; set; }
    public long ToBranchId { get; set; }
    public long? VehicleId { get; set; }
    /// <summary>Line-haul driver profile id.</summary>
    public long? DriverId { get; set; }
    public string? SealNumber { get; set; }
    public string? Notes { get; set; }
    /// <summary>Optional shipments to add immediately.</summary>
    public List<string> Codes { get; set; } = new();
}

/// <summary>Body of POST /api/v1/branch-transfers/{id}/dispatch (vehicle / driver / seal can be set or changed).</summary>
public sealed class DispatchBranchTransferRequest
{
    public long? VehicleId { get; set; }
    public long? DriverId { get; set; }
    public string? SealNumber { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Body of POST /api/v1/branch-transfers/{id}/receive.
/// ReceivedCodes empty = everything arrived. Otherwise only the listed shipments are received and the
/// others are marked Missing (the transfer becomes PartiallyReceived).
/// </summary>
public sealed class ReceiveBranchTransferRequest
{
    public List<string> ReceivedCodes { get; set; } = new();
    public string? Notes { get; set; }
}

public sealed class BranchTransferItemDto : ManifestShipmentDto
{
    public BranchTransferItemStatus ItemStatus { get; init; }
    public DateTime? ReceivedAtUtc { get; init; }
    public string? Notes { get; init; }
}

public sealed class BranchTransferDto
{
    public long Id { get; init; }
    public string TransferNumber { get; init; } = string.Empty;
    public long FromBranchId { get; init; }
    public string FromBranchName { get; init; } = string.Empty;
    public long ToBranchId { get; init; }
    public string ToBranchName { get; init; } = string.Empty;
    public long? VehicleId { get; init; }
    public string? VehiclePlateNumber { get; init; }
    public long? DriverId { get; init; }
    public string? DriverName { get; init; }
    public string? SealNumber { get; init; }
    public BranchTransferStatus Status { get; init; }
    public int ItemCount { get; init; }
    public int ReceivedCount { get; init; }
    public int MissingCount { get; init; }
    public DateTime? DispatchedAtUtc { get; init; }
    public long? DispatchedByUserId { get; init; }
    public DateTime? ReceivedAtUtc { get; init; }
    public long? ReceivedByUserId { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedByUserId { get; init; }
    /// <summary>Only in GET by id.</summary>
    public IReadOnlyList<BranchTransferItemDto>? Items { get; init; }
}

/// <summary>SortBy: createdAtUtc (default, newest first), number.</summary>
public sealed class BranchTransferListQuery : DocumentListQuery
{
    public long? FromBranchId { get; set; }
    public long? ToBranchId { get; set; }
    public BranchTransferStatus? Status { get; set; }
}

// ------------------------------------------------------------------ driver delivery manifests

public sealed class CreateDriverManifestRequest
{
    /// <summary>Driver profile id. The manifest branch is the driver's branch.</summary>
    public long DriverId { get; set; }
    /// <summary>Default: the driver's default vehicle.</summary>
    public long? VehicleId { get; set; }
    /// <summary>Planned delivery day (default today).</summary>
    public DateTime? ManifestDateUtc { get; set; }
    public string? Notes { get; set; }
    /// <summary>Optional shipments to add immediately (in this stop order).</summary>
    public List<string> Codes { get; set; } = new();
}

public sealed class CloseDriverManifestRequest
{
    /// <summary>Reason written on shipments that are still pending (they become DeliveryFailed).</summary>
    public string? PendingReason { get; set; }
    public string? Notes { get; set; }
}

public sealed class DriverManifestItemDto : ManifestShipmentDto
{
    /// <summary>Step 9: the shipping charges are collected from the recipient.</summary>
    public bool ChargesOnRecipient { get; init; }

    /// <summary>Shipping charge to collect from the recipient (0 when the sender pays).</summary>
    public decimal ChargesToCollect { get; init; }

    public int Sequence { get; init; }
    public DeliveryResult Result { get; init; }
    public DateTime? ResultAtUtc { get; init; }
    public string? FailureReason { get; init; }
    public long? ProofOfDeliveryId { get; init; }
    public string? RecipientAddressLine { get; init; }
}

public sealed class DriverManifestDto
{
    public long Id { get; init; }
    public string ManifestNumber { get; init; } = string.Empty;
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public long DriverId { get; init; }
    public string DriverName { get; init; } = string.Empty;
    public long? VehicleId { get; init; }
    public string? VehiclePlateNumber { get; init; }
    public DateTime ManifestDateUtc { get; init; }
    public DriverManifestStatus Status { get; init; }
    public int ItemCount { get; init; }
    public int DeliveredCount { get; init; }
    public int FailedCount { get; init; }
    public int PendingCount { get; init; }
    /// <summary>Total COD to collect on this manifest (information only; collection is a later step).</summary>
    public decimal TotalCodAmount { get; init; }
    public DateTime? DispatchedAtUtc { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedByUserId { get; init; }
    /// <summary>Only in GET by id.</summary>
    public IReadOnlyList<DriverManifestItemDto>? Items { get; init; }
}

/// <summary>SortBy: createdAtUtc (default, newest first), number. Drivers only get their own manifests.</summary>
public sealed class DriverManifestListQuery : DocumentListQuery
{
    public long? BranchId { get; set; }
    public long? DriverId { get; set; }
    public DriverManifestStatus? Status { get; set; }
}
