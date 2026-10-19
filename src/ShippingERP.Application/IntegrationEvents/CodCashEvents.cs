namespace ShippingERP.Application.IntegrationEvents;

// COD and cash events (step 6). Each one carries the proposed journal entry (Posting). Amounts are in the
// document currency. Accounting meaning of the main postings:
//
//   COD collected by a driver      Dr DriverCustody            Cr CodPayableToSenders
//   COD collected at the counter   Dr CashOnHand               Cr CodPayableToSenders
//   Driver handover                Dr CashOnHand               Cr DriverCustody
//   Sender settlement paid         Dr CodPayableToSenders (gross released) + Dr SenderCompensationExpense
//                                  Cr ShippingRevenue + CodCommissionRevenue + TaxPayable + OtherFeesRevenue
//                                  Cr CashOnHand / Bank (net paid)
//   => the COD amount itself is never revenue; only the deductions are.

/// <summary>COD money recorded as collected (by a driver, or at a branch counter straight into a cashbox).</summary>
public sealed record CodCollectedEvent : AccountingIntegrationEvent
{
    public override string EventType => "CodCollected";

    public long CodTransactionId { get; init; }
    public long ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public long SenderCustomerId { get; init; }
    public long? DriverId { get; init; }
    public long? CashboxId { get; init; }
    public long? ReceiptVoucherId { get; init; }
    public long CurrencyId { get; init; }
    public decimal ExpectedAmount { get; init; }
    public decimal CollectedAmount { get; init; }
    public DateTime CollectedAtUtc { get; init; }
}

/// <summary>An approved correction or reversal of a collection still held by the driver.</summary>
public sealed record CodCollectionAdjustedEvent : AccountingIntegrationEvent
{
    public override string EventType => "CodCollectionAdjusted";

    public long AdjustmentId { get; init; }
    public string AdjustmentNumber { get; init; } = string.Empty;
    public string AdjustmentType { get; init; } = string.Empty;
    public long CodTransactionId { get; init; }
    public long SenderCustomerId { get; init; }
    public long DriverId { get; init; }
    public long CurrencyId { get; init; }
    public decimal PreviousAmount { get; init; }
    public decimal NewAmount { get; init; }
}

/// <summary>A driver handed cash over to a cashbox (driver handover receipt).</summary>
public sealed record DriverCashHandedOverEvent : AccountingIntegrationEvent
{
    public override string EventType => "DriverCashHandedOver";

    public long ReceiptVoucherId { get; init; }
    public string VoucherNumber { get; init; } = string.Empty;
    public long DriverId { get; init; }
    public long CashboxId { get; init; }
    public long BranchId { get; init; }
    public long CurrencyId { get; init; }
    public decimal Amount { get; init; }
    public decimal ExpectedAmount { get; init; }
    public decimal ShortageAmount { get; init; }
    public decimal ExcessAmount { get; init; }
    public IReadOnlyList<long> CodTransactionIds { get; init; } = Array.Empty<long>();
}

/// <summary>An approved driver shortage write-off or excess cash.</summary>
public sealed record DriverCashAdjustedEvent : AccountingIntegrationEvent
{
    public override string EventType => "DriverCashAdjusted";

    public long AdjustmentId { get; init; }
    public string AdjustmentNumber { get; init; } = string.Empty;
    public string AdjustmentType { get; init; } = string.Empty;
    public long DriverId { get; init; }
    public long CurrencyId { get; init; }
    public decimal Amount { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed record SettlementEventLine(long CodTransactionId, long ShipmentId, decimal Amount, decimal GrossReleased, bool RecognizesDeductions);

/// <summary>A sender settlement was paid (cash or bank).</summary>
public sealed record SenderSettlementPaidEvent : AccountingIntegrationEvent
{
    public override string EventType => "SenderSettlementPaid";

    public long SettlementId { get; init; }
    public string SettlementNumber { get; init; } = string.Empty;
    public long SenderCustomerId { get; init; }
    public long BranchId { get; init; }
    public long CurrencyId { get; init; }
    public string PaymentMethodKind { get; init; } = string.Empty;
    public long? CashboxId { get; init; }
    public long? BankAccountId { get; init; }
    public long? CashPaymentVoucherId { get; init; }
    public string? PaymentReference { get; init; }
    public decimal TotalGrossReleased { get; init; }
    public decimal TotalShippingCharges { get; init; }
    public decimal TotalCodCommission { get; init; }
    public decimal TotalTax { get; init; }
    public decimal TotalOtherFees { get; init; }
    public decimal TotalCompensation { get; init; }
    public decimal NetPayable { get; init; }
    public DateTime PaidAtUtc { get; init; }
    public IReadOnlyList<SettlementEventLine> Lines { get; init; } = Array.Empty<SettlementEventLine>();
}

/// <summary>A paid settlement was reversed (its posting is the opposite of SenderSettlementPaid).</summary>
public sealed record SenderSettlementReversedEvent : AccountingIntegrationEvent
{
    public override string EventType => "SenderSettlementReversed";

    public long SettlementId { get; init; }
    public string SettlementNumber { get; init; } = string.Empty;
    public long SenderCustomerId { get; init; }
    public long CurrencyId { get; init; }
    public decimal NetPayable { get; init; }
    public string Reason { get; init; } = string.Empty;
}

/// <summary>A manual cash receipt voucher (customer cash deposit, other income...).</summary>
public sealed record CashReceiptPostedEvent : AccountingIntegrationEvent
{
    public override string EventType => "CashReceiptPosted";

    public long VoucherId { get; init; }
    public string VoucherNumber { get; init; } = string.Empty;
    public string ReceiptType { get; init; } = string.Empty;
    public long CashboxId { get; init; }
    public long BranchId { get; init; }
    public long CurrencyId { get; init; }
    public decimal Amount { get; init; }
    public long? CustomerId { get; init; }
    public long? CustomerPaymentId { get; init; }
}

/// <summary>A manual cash payment voucher (expense, supplier, advance...).</summary>
public sealed record CashPaymentPostedEvent : AccountingIntegrationEvent
{
    public override string EventType => "CashPaymentPosted";

    public long VoucherId { get; init; }
    public string VoucherNumber { get; init; } = string.Empty;
    public string PaymentType { get; init; } = string.Empty;
    public long CashboxId { get; init; }
    public long BranchId { get; init; }
    public long CurrencyId { get; init; }
    public decimal Amount { get; init; }
    public long? ExpenseCategoryId { get; init; }
    public long? SupplierId { get; init; }
    public long? EmployeeId { get; init; }
    public long? CustomerId { get; init; }
}

/// <summary>A receipt or payment voucher was cancelled (its posting reverses the original one).</summary>
public sealed record CashVoucherCancelledEvent : AccountingIntegrationEvent
{
    public override string EventType => "CashVoucherCancelled";

    /// <summary>"CashReceiptVoucher" or "CashPaymentVoucher".</summary>
    public string VoucherKind { get; init; } = string.Empty;
    public long VoucherId { get; init; }
    public string VoucherNumber { get; init; } = string.Empty;
    public string VoucherType { get; init; } = string.Empty;
    public long CashboxId { get; init; }
    public long CurrencyId { get; init; }
    public decimal Amount { get; init; }
    public string Reason { get; init; } = string.Empty;
}

/// <summary>A cash transfer was approved: the money left the source cashbox.</summary>
public sealed record CashTransferApprovedEvent : AccountingIntegrationEvent
{
    public override string EventType => "CashTransferApproved";

    public long TransferId { get; init; }
    public string TransferNumber { get; init; } = string.Empty;
    public string TransferType { get; init; } = string.Empty;
    public long FromCashboxId { get; init; }
    public long? ToCashboxId { get; init; }
    public long? ToBankAccountId { get; init; }
    public long CurrencyId { get; init; }
    public decimal Amount { get; init; }
    public bool Completed { get; init; }
}

/// <summary>The destination branch received a transfer that was in transit.</summary>
public sealed record CashTransferReceivedEvent : AccountingIntegrationEvent
{
    public override string EventType => "CashTransferReceived";

    public long TransferId { get; init; }
    public string TransferNumber { get; init; } = string.Empty;
    public long ToCashboxId { get; init; }
    public long CurrencyId { get; init; }
    public decimal Amount { get; init; }
}

/// <summary>The opening balance of a new cashbox.</summary>
public sealed record CashboxOpeningBalanceEvent : AccountingIntegrationEvent
{
    public override string EventType => "CashboxOpeningBalance";

    public long CashboxId { get; init; }
    public string CashboxCode { get; init; } = string.Empty;
    public long BranchId { get; init; }
    public long CurrencyId { get; init; }
    public decimal Amount { get; init; }
}

/// <summary>An approved difference between the counted and expected cash of a daily closing.</summary>
public sealed record CashboxDiscrepancyPostedEvent : AccountingIntegrationEvent
{
    public override string EventType => "CashboxDiscrepancyPosted";

    public long ClosingId { get; init; }
    public long CashboxId { get; init; }
    public long CurrencyId { get; init; }
    public DateTime BusinessDate { get; init; }
    public decimal Discrepancy { get; init; }
    public string Reason { get; init; } = string.Empty;
}
