using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Shipments;

// ------------------------------------------------------------------ returns

public sealed class CreateShipmentReturnRequest
{
    public long ShipmentId { get; set; }
    public ShipmentReturnType ReturnType { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>Where the sender gets it back (default: the origin branch).</summary>
    public long? ReturnBranchId { get; set; }
    /// <summary>Stored only (no accounting in this step).</summary>
    public decimal ReturnCharge { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Body of POST /api/v1/shipment-returns/{id}/receive (AfterDelivery returns only).</summary>
public sealed class ReceiveShipmentReturnRequest
{
    /// <summary>The branch that received the shipment back (default: the caller's branch).</summary>
    public long? BranchId { get; set; }
    public string? Notes { get; set; }
}

public sealed class CompleteShipmentReturnRequest
{
    public string? Notes { get; set; }
}

public sealed class CancelShipmentReturnRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class ShipmentReturnDto
{
    public long Id { get; init; }
    public string ReturnNumber { get; init; } = string.Empty;
    public long ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public ShipmentStatus ShipmentStatus { get; init; }
    public string ShipmentStatusName { get; init; } = string.Empty;
    public long ShipmentCurrentBranchId { get; init; }
    public ShipmentReturnType ReturnType { get; init; }
    public ShipmentReturnStatus Status { get; init; }
    public string Reason { get; init; } = string.Empty;
    public long ReturnBranchId { get; init; }
    public string ReturnBranchName { get; init; } = string.Empty;
    public decimal ReturnCharge { get; init; }
    public DateTime? ReceivedBackAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public DateTime? CancelledAtUtc { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedByUserId { get; init; }
}

/// <summary>SortBy: createdAtUtc (default, newest first), number.</summary>
public sealed class ShipmentReturnListQuery : DocumentListQuery
{
    public ShipmentReturnStatus? Status { get; set; }
    public ShipmentReturnType? ReturnType { get; set; }
    public long? ReturnBranchId { get; set; }
    public long? ShipmentId { get; set; }
}

// ------------------------------------------------------------------ damage / loss cases

public sealed class CreateDamageCaseRequest
{
    public long ShipmentId { get; set; }
    public DamageCaseType CaseType { get; set; }
    public string Description { get; set; } = string.Empty;
    /// <summary>Where it was found (default: the shipment's current branch).</summary>
    public long? BranchId { get; set; }
    public decimal EstimatedLossAmount { get; set; }
    /// <summary>
    /// True (default) changes the shipment status to LostOrDamaged. Use false for a partial damage when the
    /// shipment can still be delivered. Ignored for delivered shipments (claims after delivery).
    /// </summary>
    public bool MarkShipmentAsLostOrDamaged { get; set; } = true;
}

public sealed class InvestigateDamageCaseRequest
{
    public string? Notes { get; set; }
}

public sealed class ResolveDamageCaseRequest
{
    public DamageCaseResolution Resolution { get; set; }
    /// <summary>Required (> 0) for Compensated. Stored only.</summary>
    public decimal? CompensationAmount { get; set; }
    public string Notes { get; set; } = string.Empty;
    /// <summary>FoundAndRecovered: the branch where the shipment was found (default: the case branch).</summary>
    public long? FoundAtBranchId { get; set; }
}

public sealed class DamageCaseDto
{
    public long Id { get; init; }
    public string CaseNumber { get; init; } = string.Empty;
    public long ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public ShipmentStatus ShipmentStatus { get; init; }
    public DamageCaseType CaseType { get; init; }
    public DamageCaseStatus Status { get; init; }
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ShipmentStatus ShipmentStatusBefore { get; init; }
    public bool ShipmentMarkedLostOrDamaged { get; init; }
    public decimal EstimatedLossAmount { get; init; }
    public DamageCaseResolution? Resolution { get; init; }
    public decimal? CompensationAmount { get; init; }
    public string? ResolutionNotes { get; init; }
    public DateTime? ResolvedAtUtc { get; init; }
    public long? ResolvedByUserId { get; init; }
    public int EvidenceCount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedByUserId { get; init; }
}

/// <summary>SortBy: createdAtUtc (default, newest first), number.</summary>
public sealed class DamageCaseListQuery : DocumentListQuery
{
    public DamageCaseStatus? Status { get; set; }
    public DamageCaseType? CaseType { get; set; }
    public long? BranchId { get; set; }
    public long? ShipmentId { get; set; }
}
