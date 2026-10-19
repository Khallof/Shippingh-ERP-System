using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Billing;

/// <summary>
/// A customer invoice (فاتورة) for the shipping charges of one or more shipments of the same customer and currency.
///
/// - Created as a Draft (no number). A draft can still be changed (terms, due date, notes, invoice discount) or
///   cancelled. Its lines already reserve the shipments, so they cannot be put on a second invoice.
/// - Issue: gets the number INV-2026-000001 (generated safely inside the transaction), the invoice and due dates,
///   and a debit in the customer sub-ledger. From now on the invoice is never edited: mistakes are corrected with
///   credit notes, or by cancelling it while nothing was paid or credited.
/// - Payments (allocations) and credit notes reduce BalanceDue; the status follows automatically.
///
/// Amounts:
///   SubTotal          = sum of line shipping charges (gross: shipping subtotal + COD commission)
///   DiscountAmount    = LineDiscountAmount (from the shipments) + InvoiceDiscountAmount (given on the draft)
///   TaxableAmount     = SubTotal - DiscountAmount
///   TotalAmount       = TaxableAmount + TaxAmount
///   BalanceDue        = TotalAmount - PaidAmount - CreditAppliedAmount
/// RowVersion protects the balance against two payments allocated at the same moment.
/// </summary>
public class Invoice : BaseEntity
{
    /// <summary>INV-2026-000001. Null while the invoice is a draft.</summary>
    public string? InvoiceNumber { get; set; }

    public long CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    /// <summary>Issuing branch (branch users only see the invoices of their branch).</summary>
    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public long CurrencyId { get; set; }

    public Currency Currency { get; set; } = null!;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    /// <summary>True when created with POST /invoices/consolidated (several shipments).</summary>
    public bool IsConsolidated { get; set; }

    /// <summary>Set when issued (defaults to the issue date).</summary>
    public DateTime? InvoiceDateUtc { get; set; }

    /// <summary>Days between the invoice date and the due date (0 = due on issue).</summary>
    public int PaymentTermsDays { get; set; }

    /// <summary>Set when issued: InvoiceDateUtc + PaymentTermsDays (or the explicit due date of the draft).</summary>
    public DateTime? DueDateUtc { get; set; }

    // ---------------- Amounts ----------------

    public decimal SubTotal { get; set; }

    public decimal LineDiscountAmount { get; set; }

    public decimal InvoiceDiscountAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    /// <summary>Sum of the payment allocations.</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>Sum of all credit note totals (may exceed the balance; the excess becomes customer credit).</summary>
    public decimal CreditedAmount { get; set; }

    /// <summary>Part of the credit notes that reduced this invoice's balance.</summary>
    public decimal CreditAppliedAmount { get; set; }

    public decimal BalanceDue { get; set; }

    public string? Notes { get; set; }

    // ---------------- Milestones ----------------

    public DateTime? IssuedAtUtc { get; set; }

    public long? IssuedByUserId { get; set; }

    /// <summary>When the balance reached 0 by payments.</summary>
    public DateTime? PaidAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public long? CancelledByUserId { get; set; }

    public string? CancellationReason { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();

    public ICollection<CustomerPaymentAllocation> PaymentAllocations { get; set; } = new List<CustomerPaymentAllocation>();

    public ICollection<CreditNote> CreditNotes { get; set; } = new List<CreditNote>();
}
