using ShippingERP.Domain.Enums;
using S = ShippingERP.Domain.Enums.ShipmentStatus;
using Src = ShippingERP.Domain.Enums.ShipmentStatusChangeSource;

namespace ShippingERP.Domain.Shipments;

/// <summary>
/// The shipment state machine. Every status change in the system goes through <see cref="CanTransition"/>.
///
/// Three layers of rules:
/// 1. <b>Allowed next statuses</b> (the table below). Delivered, Returned, Cancelled and LostOrDamaged are final.
/// 2. <b>Workflow-only statuses</b>: some statuses can only be set by their workflow
///    (InTransit only by a branch transfer, OutForDelivery only by a driver manifest, Delivered only with a
///    proof of delivery...). A user cannot jump there with the manual status endpoint.
/// 3. <b>Special workflows</b> that reopen a final status:
///    Delivered -> ReceivedAtBranch only through the return workflow (the recipient sends it back), and
///    LostOrDamaged -> ReceivedAtBranch only when a damage / loss case is resolved as "found".
///
/// A status <b>correction</b> (Source = Correction, separate permission, reason required) bypasses 1-3, because
/// people make mistakes; it is still recorded in the immutable history with IsCorrection = true.
/// </summary>
public static class ShipmentStatusRules
{
    private static readonly IReadOnlyDictionary<S, S[]> AllowedNext = new Dictionary<S, S[]>
    {
        [S.Created] = new[] { S.PickedUp, S.ReceivedAtBranch, S.InWarehouse, S.Cancelled, S.LostOrDamaged },
        [S.PickedUp] = new[] { S.ReceivedAtBranch, S.InWarehouse, S.Cancelled, S.LostOrDamaged },
        [S.ReceivedAtBranch] = new[] { S.InWarehouse, S.InTransit, S.OutForDelivery, S.Delivered, S.Returned, S.Cancelled, S.LostOrDamaged },
        [S.InWarehouse] = new[] { S.ReceivedAtBranch, S.InTransit, S.OutForDelivery, S.Delivered, S.Returned, S.Cancelled, S.LostOrDamaged },
        [S.InTransit] = new[] { S.ReceivedAtBranch, S.ArrivedAtDestination, S.LostOrDamaged },
        [S.ArrivedAtDestination] = new[] { S.InWarehouse, S.InTransit, S.OutForDelivery, S.Delivered, S.Returned, S.LostOrDamaged },
        [S.OutForDelivery] = new[] { S.Delivered, S.DeliveryFailed, S.LostOrDamaged },
        [S.DeliveryFailed] = new[] { S.ReceivedAtBranch, S.InWarehouse, S.InTransit, S.OutForDelivery, S.Delivered, S.Returned, S.LostOrDamaged },
        [S.Delivered] = Array.Empty<S>(),
        [S.Returned] = Array.Empty<S>(),
        [S.Cancelled] = Array.Empty<S>(),
        [S.LostOrDamaged] = Array.Empty<S>()
    };

    /// <summary>Statuses that only a specific workflow may set.</summary>
    private static readonly IReadOnlyDictionary<S, Src[]> RequiredSources = new Dictionary<S, Src[]>
    {
        [S.InTransit] = new[] { Src.BranchTransfer },
        [S.ArrivedAtDestination] = new[] { Src.BranchTransfer },
        [S.OutForDelivery] = new[] { Src.DriverManifest },
        [S.Delivered] = new[] { Src.Delivery },
        [S.DeliveryFailed] = new[] { Src.Delivery, Src.DriverManifest },
        [S.Returned] = new[] { Src.Return },
        [S.Cancelled] = new[] { Src.Cancellation },
        [S.LostOrDamaged] = new[] { Src.DamageCase }
    };

    /// <summary>Changes that reopen a final status, each allowed from one workflow only.</summary>
    private static readonly HashSet<(S From, S To, Src Source)> SpecialWorkflows = new()
    {
        (S.Delivered, S.ReceivedAtBranch, Src.Return),
        (S.LostOrDamaged, S.ReceivedAtBranch, Src.DamageCase)
    };

    /// <summary>Statuses the manual status endpoint (PATCH /shipments/{id}/status) may set.</summary>
    public static IReadOnlyCollection<S> ManualTargets { get; } = new[] { S.PickedUp, S.ReceivedAtBranch, S.InWarehouse };

    /// <summary>Statuses from which a shipment can still be cancelled (it has not left the origin branch).</summary>
    public static IReadOnlyCollection<S> CancellableStatuses { get; } = new[] { S.Created, S.PickedUp, S.ReceivedAtBranch, S.InWarehouse };

    /// <summary>Statuses in which the shipment details (recipient, packages, prices) can still be edited.</summary>
    public static IReadOnlyCollection<S> EditableStatuses { get; } = new[] { S.Created, S.PickedUp, S.ReceivedAtBranch, S.InWarehouse };

    /// <summary>Statuses in which a shipment is physically at a branch and can be put on a transfer or a driver manifest.</summary>
    public static IReadOnlyCollection<S> AtBranchStatuses { get; } = new[] { S.ReceivedAtBranch, S.InWarehouse, S.ArrivedAtDestination, S.DeliveryFailed };

    public static bool IsFinal(S status) => AllowedNext[status].Length == 0;

    /// <summary>Statuses reachable from <paramref name="from"/> by any normal workflow (without correction).</summary>
    public static IReadOnlyCollection<S> NextStatuses(S from) => AllowedNext[from];

    public static bool CanTransition(S from, S to, Src source)
    {
        if (from == to || to == S.Created)
        {
            return false;
        }

        // A correction may set any status (except Created). The caller must check the permission and the reason.
        if (source == Src.Correction)
        {
            return true;
        }

        if (SpecialWorkflows.Contains((from, to, source)))
        {
            return true;
        }

        if (!AllowedNext[from].Contains(to))
        {
            return false;
        }

        if (RequiredSources.TryGetValue(to, out var sources) && !sources.Contains(source))
        {
            return false;
        }

        // A shipment on the road can only be received by its transfer (or reported lost).
        if (from == S.InTransit && to != S.LostOrDamaged && source != Src.BranchTransfer)
        {
            return false;
        }

        // A shipment with a driver comes back only through the delivery result (or a loss report).
        if (from == S.OutForDelivery && to != S.LostOrDamaged && source is not (Src.Delivery or Src.DriverManifest))
        {
            return false;
        }

        return true;
    }
}
