using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Billing;

/// <summary>
/// One billed charge of a shipment. Every line references a shipment and the pricing snapshot it billed.
///
/// Double billing is impossible: a unique index allows only ONE active line per (shipment, charge type).
/// The line becomes inactive only when its invoice is cancelled, which frees the shipment for a new invoice.
///
///   TaxableAmount = ShippingCharge - LineDiscountAmount - InvoiceDiscountAmount
///   LineTotal     = TaxableAmount + TaxAmount
/// </summary>
public class InvoiceLine : BaseEntity
{
    public long InvoiceId { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public int LineNumber { get; set; }

    public long ShipmentId { get; set; }

    public Shipment Shipment { get; set; } = null!;

    public long PricingSnapshotId { get; set; }

    public ShipmentPricingSnapshot PricingSnapshot { get; set; } = null!;

    public InvoiceChargeType ChargeType { get; set; } = InvoiceChargeType.ShippingCharge;

    public string Description { get; set; } = string.Empty;

    /// <summary>Gross charge of the snapshot (shipping subtotal + COD commission).</summary>
    public decimal ShippingCharge { get; set; }

    /// <summary>Discount already on the shipment (contract + manual).</summary>
    public decimal LineDiscountAmount { get; set; }

    /// <summary>This line's share of the invoice discount given on the draft.</summary>
    public decimal InvoiceDiscountAmount { get; set; }

    public decimal TaxableAmount { get; set; }

    public long? TaxRateId { get; set; }

    public decimal TaxPercentage { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }

    /// <summary>Taxable amount already credited by credit notes.</summary>
    public decimal CreditedTaxableAmount { get; set; }

    public decimal CreditedTaxAmount { get; set; }

    /// <summary>False once the invoice is cancelled (the shipment can be invoiced again).</summary>
    public bool IsActive { get; set; } = true;
}
