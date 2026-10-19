namespace ShippingERP.Domain.Enums;

// Enums of cashboxes, cash vouchers and cash transfers (step 6). Stored as text, so never rename a value.

/// <summary>Cashbox sub-ledger entries (CashboxTransaction). In = AmountIn, Out = AmountOut.</summary>
public enum CashboxTransactionType
{
    /// <summary>In: the balance entered when the cashbox was created.</summary>
    OpeningBalance = 1,

    /// <summary>In: a driver handed over collected COD cash.</summary>
    DriverHandover = 2,

    /// <summary>In: a recipient paid COD at the branch counter.</summary>
    CounterCodCollection = 3,

    /// <summary>In: a cash receipt voucher (customer cash payment, other income...).</summary>
    CashReceipt = 4,

    /// <summary>Out: a cash payment voucher (expense, supplier...).</summary>
    CashPayment = 5,

    /// <summary>Out: net COD amount paid to a sender.</summary>
    SenderSettlementPayment = 6,

    /// <summary>Out: cash sent to another cashbox, branch or bank.</summary>
    TransferOut = 7,

    /// <summary>In: cash received from another cashbox or branch.</summary>
    TransferIn = 8,

    /// <summary>Out: approved shortage found at the daily closing count.</summary>
    ClosingShortage = 9,

    /// <summary>In: approved excess found at the daily closing count.</summary>
    ClosingExcess = 10,

    /// <summary>In or out: cancels an earlier transaction (cancelled voucher, reversed settlement...).</summary>
    Reversal = 11
}

/// <summary>Why money entered the cashbox (سند قبض).</summary>
public enum CashReceiptType
{
    /// <summary>System: created by POST /cod/driver-handover.</summary>
    DriverHandover = 1,

    /// <summary>System: created by POST /cod/collect with a cashbox (recipient paid at the counter).</summary>
    CounterCodCollection = 2,

    /// <summary>Deposits the cash of an existing customer payment (step 5) into this cashbox.</summary>
    CustomerPayment = 3,

    OtherIncome = 4,

    Other = 99
}

/// <summary>Why money left the cashbox (سند صرف).</summary>
public enum CashPaymentType
{
    /// <summary>System: created by POST /cod/settlements/{id}/pay.</summary>
    SenderSettlement = 1,

    Expense = 2,

    SupplierPayment = 3,

    EmployeeAdvance = 4,

    CustomerRefund = 5,

    Other = 99
}

public enum CashVoucherStatus
{
    Posted = 1,

    /// <summary>Cancelled with a reason; a reversing cashbox transaction was added.</summary>
    Cancelled = 2
}

public enum CashTransferType
{
    /// <summary>Two cashboxes of the same branch: completed when approved.</summary>
    BetweenCashboxes = 1,

    /// <summary>To a cashbox of another branch: in transit until the destination branch receives it.</summary>
    BetweenBranches = 2,

    /// <summary>Cash deposited into a bank account: completed when approved (bank deposit reference required).</summary>
    CashboxToBank = 3
}

/// <summary>
/// PendingApproval -> Approved by a second user (money leaves the source cashbox) -> InTransit (another branch) ->
/// Completed when received. Same-branch and bank transfers go straight to Completed on approval.
/// PendingApproval -> Rejected / Cancelled.
/// </summary>
public enum CashTransferStatus
{
    PendingApproval = 1,
    InTransit = 2,
    Completed = 3,
    Rejected = 4,
    Cancelled = 5
}

/// <summary>
/// Daily closing of a cashbox. Closed = counted = expected. A difference needs a reason and a second user's
/// approval (PendingApproval); approval posts the difference (Approved), rejection asks for a new count (Rejected).
/// </summary>
public enum CashboxClosingStatus
{
    Closed = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4
}
