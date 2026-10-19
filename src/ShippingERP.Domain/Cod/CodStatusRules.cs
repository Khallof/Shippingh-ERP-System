using ShippingERP.Domain.Enums;
using S = ShippingERP.Domain.Enums.CODStatus;

namespace ShippingERP.Domain.Cod;

/// <summary>
/// State machine of the COD money of a shipment (pure rules, no database: unit-tested directly).
///
///   Pending <-> AssignedToDriver -> CollectedByDriver -> ReceivedAtBranchCashbox -> ReadyForSenderSettlement
///                                       (or counter collection straight to ReceivedAtBranchCashbox)
///   ReadyForSenderSettlement -> PartiallySettled -> SettledToSender (payments; a reversal goes back)
///   Any collected state -> Disputed -> back to the state before the dispute
///   Pending / AssignedToDriver -> Cancelled -> Pending (reopened)
/// Corrections go backwards only through their own workflows (cancelled handover voucher, approved collection
/// reversal, reversed settlement).
/// </summary>
public static class CodStatusRules
{
    private static readonly IReadOnlyDictionary<S, S[]> Allowed = new Dictionary<S, S[]>
    {
        [S.Pending] = new[] { S.AssignedToDriver, S.CollectedByDriver, S.ReceivedAtBranchCashbox, S.Cancelled },
        [S.AssignedToDriver] = new[] { S.Pending, S.CollectedByDriver, S.ReceivedAtBranchCashbox, S.Cancelled },
        [S.CollectedByDriver] = new[] { S.ReceivedAtBranchCashbox, S.Disputed, S.Pending, S.AssignedToDriver },
        [S.ReceivedAtBranchCashbox] = new[] { S.ReadyForSenderSettlement, S.Disputed, S.CollectedByDriver, S.Pending, S.AssignedToDriver },
        [S.ReadyForSenderSettlement] = new[] { S.PartiallySettled, S.SettledToSender, S.Disputed },
        [S.PartiallySettled] = new[] { S.PartiallySettled, S.SettledToSender, S.ReadyForSenderSettlement, S.Disputed },
        [S.SettledToSender] = new[] { S.PartiallySettled, S.ReadyForSenderSettlement },
        [S.Disputed] = new[] { S.CollectedByDriver, S.ReceivedAtBranchCashbox, S.ReadyForSenderSettlement, S.PartiallySettled, S.Pending, S.AssignedToDriver },
        [S.Cancelled] = new[] { S.Pending, S.AssignedToDriver }
    };

    /// <summary>Statuses the automatic shipment synchronization may change (nothing collected yet).</summary>
    public static IReadOnlyCollection<S> NotCollectedStatuses { get; } = new[] { S.Pending, S.AssignedToDriver, S.Cancelled };

    /// <summary>Statuses where the money is out of the recipient's hands and not fully paid to the sender.</summary>
    public static IReadOnlyCollection<S> HeldStatuses { get; } =
        new[] { S.CollectedByDriver, S.ReceivedAtBranchCashbox, S.ReadyForSenderSettlement, S.PartiallySettled, S.Disputed };

    /// <summary>Statuses whose remaining net amount can be put in a sender settlement.</summary>
    public static IReadOnlyCollection<S> SettleableStatuses { get; } = new[] { S.ReadyForSenderSettlement, S.PartiallySettled };

    /// <summary>Statuses from which a dispute can be opened.</summary>
    public static IReadOnlyCollection<S> DisputableStatuses { get; } =
        new[] { S.CollectedByDriver, S.ReceivedAtBranchCashbox, S.ReadyForSenderSettlement, S.PartiallySettled };

    public static bool CanTransition(S from, S to) => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static bool IsNotCollected(S status) => NotCollectedStatuses.Contains(status);

    public static bool CanDispute(S status) => DisputableStatuses.Contains(status);

    public static bool IsSettleable(S status) => SettleableStatuses.Contains(status);

    /// <summary>
    /// True while the collection itself can still be corrected or reversed: the money is with the driver
    /// (directly, or disputed while with the driver).
    /// </summary>
    public static bool IsWithDriver(S status, S? statusBeforeDispute) =>
        status == S.CollectedByDriver || (status == S.Disputed && statusBeforeDispute == S.CollectedByDriver);

    /// <summary>The status a not-collected COD should have for its shipment (used by the synchronization).</summary>
    public static S NotCollectedStatusFor(ShipmentStatus shipmentStatus, bool hasDriver, S current) =>
        shipmentStatus switch
        {
            ShipmentStatus.OutForDelivery when hasDriver => S.AssignedToDriver,

            // Delivered: the driver still has to record the money; keep who had it.
            ShipmentStatus.Delivered => current == S.Cancelled ? S.Pending : current,

            _ => S.Pending
        };
}
