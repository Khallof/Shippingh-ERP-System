namespace ShippingERP.Domain.Enums;

// Enums of customer invoicing and receivables (step 5). Stored as text, so never rename a value.

/// <summary>
/// Invoice life cycle (see Domain/Billing/InvoiceStatusRules for the allowed transitions).
///
/// Draft          -> can be changed (due date, terms, notes, invoice discount) or cancelled; has no number yet.
/// Issued         -> final: has a number and a customer-account debit. Never edited again.
/// PartiallyPaid  -> part of the balance was paid or credited.
/// Paid           -> balance = 0.
/// Overdue        -> balance > 0 and the due date has passed.
/// Cancelled      -> a draft, or an issued invoice without payments / credit notes, that was voided.
/// Credited       -> credit notes cover the whole invoice total.
/// </summary>
public enum InvoiceStatus
{
    Draft = 1,
    Issued = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Overdue = 5,
    Cancelled = 6,
    Credited = 7
}

/// <summary>
/// The kind of charge an invoice line bills. A shipment can be billed once per charge type
/// (enforced by a unique index on active lines).
/// </summary>
public enum InvoiceChargeType
{
    /// <summary>The shipping charge of the shipment's current pricing snapshot (fees, COD commission, tax).</summary>
    ShippingCharge = 1
}

/// <summary>Customer sub-ledger entry types (CustomerAccountTransaction). Debit = the customer owes more.</summary>
public enum CustomerAccountTransactionType
{
    /// <summary>Debit: an invoice was issued.</summary>
    InvoiceIssued = 1,

    /// <summary>Credit: an issued invoice was cancelled (reverses InvoiceIssued).</summary>
    InvoiceCancelled = 2,

    /// <summary>Credit: the customer paid us.</summary>
    PaymentReceived = 3,

    /// <summary>Credit: a credit note reduced what the customer owes.</summary>
    CreditNoteIssued = 4
}
