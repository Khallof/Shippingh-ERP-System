using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Shipments;

/// <summary>
/// Damage or loss case (حالة تلف أو فقدان), number DLC-2026-000001.
/// Opening a case can mark the shipment LostOrDamaged. Resolving it records the outcome; "FoundAndRecovered"
/// brings a lost shipment back to ReceivedAtBranch. Amounts are stored only (no accounting yet).
/// Evidence photos are ShipmentAttachments with DamageCaseId.
/// </summary>
public class ShipmentDamageOrLossCase : BaseEntity
{
    public string CaseNumber { get; set; } = string.Empty;

    public long ShipmentId { get; set; }

    public Shipment Shipment { get; set; } = null!;

    public DamageCaseType CaseType { get; set; }

    public DamageCaseStatus Status { get; set; } = DamageCaseStatus.Open;

    /// <summary>The branch where the damage or loss was found.</summary>
    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public string Description { get; set; } = string.Empty;

    /// <summary>The shipment status before the case (restored information for the timeline).</summary>
    public ShipmentStatus ShipmentStatusBefore { get; set; }

    /// <summary>True when opening the case changed the shipment to LostOrDamaged.</summary>
    public bool ShipmentMarkedLostOrDamaged { get; set; }

    public decimal EstimatedLossAmount { get; set; }

    public DamageCaseResolution? Resolution { get; set; }

    public decimal? CompensationAmount { get; set; }

    public string? ResolutionNotes { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public long? ResolvedByUserId { get; set; }
}
