using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.Billing;

/// <summary>
/// The credited part of one invoice line. TaxAmount uses the tax percentage of the invoice line, so the tax is
/// reversed at the same rate it was charged. Append-only.
/// </summary>
public class CreditNoteLine : BaseEntity, IAppendOnlyEntity
{
    public long CreditNoteId { get; set; }

    public CreditNote CreditNote { get; set; } = null!;

    public long InvoiceLineId { get; set; }

    public InvoiceLine InvoiceLine { get; set; } = null!;

    /// <summary>Copied from the invoice line for reporting.</summary>
    public long ShipmentId { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal TaxableAmount { get; set; }

    public decimal TaxPercentage { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }
}
