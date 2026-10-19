using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.MasterData;

namespace ShippingERP.Domain.Entities.Billing;

/// <summary>
/// A credit note (إشعار دائن), number CRN-2026-000001, issued against an issued invoice to reduce what the customer
/// owes (wrong price, service failure, goodwill...). It is final when created (append-only) and credits the
/// customer sub-ledger.
///
/// AppliedToInvoiceAmount reduces the invoice balance. When the invoice is already (partly) paid, the rest
/// (UnappliedAmount) stays as customer credit in the sub-ledger (refund or use is handled by accounting later).
/// </summary>
public class CreditNote : BaseEntity, IAppendOnlyEntity
{
    public string CreditNoteNumber { get; set; } = string.Empty;

    public long InvoiceId { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public long CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public long CurrencyId { get; set; }

    public Currency Currency { get; set; } = null!;

    public DateTime CreditNoteDateUtc { get; set; }

    public string Reason { get; set; } = string.Empty;

    public decimal TaxableAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal AppliedToInvoiceAmount { get; set; }

    public decimal UnappliedAmount { get; set; }

    public ICollection<CreditNoteLine> Lines { get; set; } = new List<CreditNoteLine>();
}
