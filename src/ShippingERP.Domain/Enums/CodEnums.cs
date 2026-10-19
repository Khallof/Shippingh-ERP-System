namespace ShippingERP.Domain.Enums;

// Enums of cash on delivery (step 6). All are stored as text, so never rename a value; add new ones at the end.

/// <summary>
/// Life cycle of the COD amount of one shipment (see Domain/Cod/CodStatusRules for the allowed changes).
///
/// Pending                  -> the shipment is not out for delivery yet (or came back from a failed attempt).
/// AssignedToDriver         -> the shipment is out for delivery with a driver (driver manifest dispatched).
/// CollectedByDriver        -> the driver recorded the money; it is in the driver's custody (عهدة السائق).
/// ReceivedAtBranchCashbox  -> the money was handed over to a branch cashbox (or collected at the counter).
/// ReadyForSenderSettlement -> reviewed: the deductions are calculated and the net can be paid to the sender.
/// PartiallySettled         -> part of the net amount was paid to the sender.
/// SettledToSender          -> the whole net amount was paid to the sender.
/// Cancelled                -> the shipment was cancelled / returned before collection, or COD was removed.
/// Disputed                 -> blocked until a supervisor resolves it (wrong amount, customer complaint...).
/// </summary>
public enum CODStatus
{
    Pending = 1,
    AssignedToDriver = 2,
    CollectedByDriver = 3,
    ReceivedAtBranchCashbox = 4,
    ReadyForSenderSettlement = 5,
    PartiallySettled = 6,
    SettledToSender = 7,
    Cancelled = 8,
    Disputed = 9
}

/// <summary>What happened to a COD transaction (one immutable CODTransactionHistory row per event).</summary>
public enum CODTransactionType
{
    /// <summary>The COD shipment was registered (created automatically with the shipment).</summary>
    Registered = 1,

    AssignedToDriver = 2,

    /// <summary>Back to Pending (failed delivery, back in the warehouse...).</summary>
    Unassigned = 3,

    /// <summary>The COD amount of the shipment changed before collection.</summary>
    ExpectedAmountChanged = 4,

    Cancelled = 5,

    /// <summary>A cancelled COD became active again (the shipment or its COD flag was restored).</summary>
    Reopened = 6,

    /// <summary>The driver recorded the collected money.</summary>
    Collected = 7,

    /// <summary>The recipient paid at the branch counter (straight into a cashbox).</summary>
    CollectedAtCounter = 8,

    /// <summary>The driver handed the money over to a cashbox.</summary>
    HandedOver = 9,

    /// <summary>The handover / counter receipt voucher was cancelled.</summary>
    HandoverReversed = 10,

    MarkedReady = 11,

    SettlementPaid = 12,

    SettlementReversed = 13,

    /// <summary>Approved adjustment: the collected amount was corrected.</summary>
    CollectionCorrected = 14,

    /// <summary>Approved adjustment: the collection was recorded by mistake and removed.</summary>
    CollectionReversed = 15,

    /// <summary>Approved adjustment: an extra fee will be deducted from the sender (return, storage, damage...).</summary>
    SenderFeeAdded = 16,

    /// <summary>Approved adjustment: an extra amount will be paid to the sender (compensation).</summary>
    SenderCompensationAdded = 17,

    DisputeOpened = 18,

    DisputeResolved = 19
}

/// <summary>Driver cash sub-ledger entries. Debit = the driver holds more money of the company.</summary>
public enum DriverCashMovementType
{
    /// <summary>Debit: COD collected from a recipient.</summary>
    CodCollected = 1,

    /// <summary>Credit: cash handed over to a cashbox.</summary>
    Handover = 2,

    /// <summary>Debit: a handover voucher was cancelled; the money is again with the driver.</summary>
    HandoverReversed = 3,

    /// <summary>Debit or credit: approved correction of a collected amount.</summary>
    CollectionCorrected = 4,

    /// <summary>Credit: approved removal of a collection recorded by mistake.</summary>
    CollectionReversed = 5,

    /// <summary>Credit: approved write-off of a cash shortage (charged to the driver or a loss, decided by accounting).</summary>
    ShortageWrittenOff = 6,

    /// <summary>Debit: approved excess cash the driver brought (so it can be handed over).</summary>
    ExcessRecorded = 7
}

/// <summary>Corrections that need a reason and the approval of a second user.</summary>
public enum CODAdjustmentType
{
    /// <summary>Reduces the driver balance (missing cash written off). Needs DriverId.</summary>
    DriverShortageWriteOff = 1,

    /// <summary>Increases the driver balance (the driver brought more cash than recorded). Needs DriverId.</summary>
    DriverExcessCash = 2,

    /// <summary>Changes the collected amount of a COD still with the driver. Needs CODTransactionId + NewCollectedAmount.</summary>
    CollectionCorrection = 3,

    /// <summary>Removes a collection recorded on the wrong shipment (COD back to Pending). Needs CODTransactionId.</summary>
    CollectionReversal = 4,

    /// <summary>Extra fee deducted from the sender's net amount (return, storage, damaged goods...). Needs CODTransactionId.</summary>
    SenderFee = 5,

    /// <summary>Extra amount added to the sender's net amount (compensation for damage / delay). Needs CODTransactionId.</summary>
    SenderCompensation = 6
}

public enum CODAdjustmentStatus
{
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>
/// Sender settlement (تسوية المرسل) life cycle.
/// Draft -> Approved -> Paid. Draft / Approved -> Cancelled. Paid -> Reversed (payment recorded by mistake).
/// </summary>
public enum SettlementStatus
{
    Draft = 1,
    Approved = 2,
    Paid = 3,
    Cancelled = 4,
    Reversed = 5
}
