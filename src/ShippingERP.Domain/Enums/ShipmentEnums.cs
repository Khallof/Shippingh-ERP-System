namespace ShippingERP.Domain.Enums;

// Enums used by shipments and their documents (step 4). All are stored as text, so never rename a value.

/// <summary>What caused a status change. Some statuses can only be reached from a specific workflow.</summary>
public enum ShipmentStatusChangeSource
{
    /// <summary>The shipment was registered (POST /shipments).</summary>
    Creation = 1,

    /// <summary>Imported from an Excel file.</summary>
    Import = 2,

    /// <summary>A user scanned / changed the status directly (PickedUp, ReceivedAtBranch, InWarehouse).</summary>
    Manual = 3,

    /// <summary>Warehouse check-in / check-out manifest.</summary>
    WarehouseManifest = 4,

    /// <summary>Branch transfer dispatched / received.</summary>
    BranchTransfer = 5,

    /// <summary>Driver delivery manifest dispatched / closed.</summary>
    DriverManifest = 6,

    /// <summary>Delivery confirmed (proof of delivery) or delivery attempt failed.</summary>
    Delivery = 7,

    /// <summary>Return-to-sender workflow.</summary>
    Return = 8,

    /// <summary>Damage / loss case opened or resolved.</summary>
    DamageCase = 9,

    /// <summary>Shipment cancelled.</summary>
    Cancellation = 10,

    /// <summary>Explicit status correction by an authorized user (bypasses the normal rules, reason required).</summary>
    Correction = 11
}

/// <summary>Kind of file attached to a shipment.</summary>
public enum ShipmentAttachmentType
{
    Document = 1,
    PackagePhoto = 2,
    CommercialInvoice = 3,
    /// <summary>Recipient signature (proof of delivery).</summary>
    Signature = 4,
    /// <summary>Photo taken at delivery (proof of delivery).</summary>
    DeliveryPhoto = 5,
    /// <summary>Evidence for a damage / loss case.</summary>
    DamageEvidence = 6,
    Other = 99
}

/// <summary>How the shipping charge was obtained.</summary>
public enum PricingSource
{
    /// <summary>Calculated from a pricing rule of the customer's plan (or the default plan).</summary>
    PricingRule = 1,

    /// <summary>Entered manually by a user (no rule matched, or a special price).</summary>
    Manual = 2,

    /// <summary>Calculated from a rule of the sender's own contract (a pricing plan linked to that customer).</summary>
    CustomerContract = 3,

    /// <summary>Step 9: a price per kg or per m³ typed on the shipment (quantity x unit price, no rule needed).</summary>
    UnitPrice = 4
}

/// <summary>Direction of a warehouse movement manifest.</summary>
public enum WarehouseMovementType
{
    /// <summary>إدخال: shipments enter the warehouse (status becomes InWarehouse).</summary>
    CheckIn = 1,

    /// <summary>إخراج: shipments leave the warehouse back to the branch floor (status becomes ReceivedAtBranch).</summary>
    CheckOut = 2
}

public enum WarehouseManifestStatus
{
    /// <summary>Items can still be added or removed.</summary>
    Draft = 1,

    /// <summary>Applied to the shipments; cannot change anymore.</summary>
    Confirmed = 2,

    Cancelled = 3
}

public enum BranchTransferStatus
{
    Draft = 1,

    /// <summary>Left the origin branch; every shipment is InTransit.</summary>
    Dispatched = 2,

    /// <summary>Every shipment was received by the destination branch.</summary>
    Received = 3,

    /// <summary>Received, but some shipments were missing.</summary>
    PartiallyReceived = 4,

    Cancelled = 5
}

public enum BranchTransferItemStatus
{
    Pending = 1,
    InTransit = 2,
    Received = 3,
    /// <summary>Not found when the transfer was received. Open a damage / loss case to follow it up.</summary>
    Missing = 4
}

public enum DriverManifestStatus
{
    Draft = 1,

    /// <summary>Handed to the driver; every shipment is OutForDelivery.</summary>
    Dispatched = 2,

    /// <summary>The driver came back; every item has a result.</summary>
    Closed = 3,

    Cancelled = 4
}

public enum DeliveryResult
{
    Pending = 1,
    Delivered = 2,
    Failed = 3
}

public enum ShipmentReturnType
{
    /// <summary>The delivery failed (absent, wrong address...).</summary>
    FailedDelivery = 1,

    /// <summary>The recipient refused the shipment.</summary>
    RecipientRefused = 2,

    /// <summary>The sender asked to get the shipment back.</summary>
    SenderRequest = 3,

    /// <summary>The recipient sends a delivered shipment back (the only way to reopen a delivered shipment).</summary>
    AfterDelivery = 4
}

public enum ShipmentReturnStatus
{
    Open = 1,
    Completed = 2,
    Cancelled = 3
}

public enum DamageCaseType
{
    Damaged = 1,
    PartiallyDamaged = 2,
    Lost = 3
}

public enum DamageCaseStatus
{
    Open = 1,
    UnderInvestigation = 2,
    Resolved = 3
}

public enum DamageCaseResolution
{
    /// <summary>The customer will be compensated (the amount is stored; accounting comes in a later step).</summary>
    Compensated = 1,

    /// <summary>The claim was rejected.</summary>
    Rejected = 2,

    /// <summary>The lost shipment was found; it goes back to ReceivedAtBranch.</summary>
    FoundAndRecovered = 3,

    /// <summary>Recorded only, no further action.</summary>
    NoAction = 4
}

/// <summary>
/// Step 9: how the shipping charge of a shipment is calculated. Chosen on every shipment.
/// Weight = by kilogram (the weight tiers of the price rule); Volume = by cubic metre (PriceRule.PricePerCubicMeter).
/// </summary>
public enum PricingBasis
{
    Weight = 1,
    Volume = 2
}

/// <summary>Step 9: who pays the shipping charge (who is invoiced for it).</summary>
public enum ChargesPayer
{
    /// <summary>The sender is invoiced (prepaid, the default before step 9).</summary>
    Sender = 1,

    /// <summary>The recipient pays on delivery (freight collect): the recipient is invoiced.</summary>
    Recipient = 2
}

public static class ShipmentChargeEnums
{
    public static string Arabic(PricingBasis basis) => basis switch
    {
        PricingBasis.Weight => "بالوزن",
        PricingBasis.Volume => "بالحجم",
        _ => basis.ToString()
    };

    public static string Arabic(ChargesPayer payer) => payer switch
    {
        ChargesPayer.Sender => "على المرسل",
        ChargesPayer.Recipient => "على المستلم",
        _ => payer.ToString()
    };
}
