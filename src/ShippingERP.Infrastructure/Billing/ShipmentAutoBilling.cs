using Microsoft.Extensions.Options;

namespace ShippingERP.Infrastructure.Billing;

/// <summary>Configuration section "Billing" (step 9).</summary>
public sealed class ShipmentBillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>
    /// True (default): every new shipment is invoiced and issued at once to the customer who pays the charges, so its
    /// total becomes that customer's debt immediately; a payment (autoallocated) reduces it. Editing or cancelling the
    /// shipment cancels its single-shipment invoice and (when editing) issues a new one.
    /// False: invoices are created by hand (the step 5 behaviour).
    /// </summary>
    public bool AutoInvoiceShipments { get; set; } = true;
}

/// <summary>
/// Step 9: "the shipment total is a debt of the customer, a payment reduces it". Uses the normal invoices under the
/// hood (customer ledger, statement, aging and accounting stay correct); the user never has to create one.
/// </summary>
public sealed class ShipmentAutoBilling
{
    private readonly InvoiceService _invoices;
    private readonly ShipmentBillingOptions _options;

    public ShipmentAutoBilling(InvoiceService invoices, IOptions<ShipmentBillingOptions> options)
    {
        _invoices = invoices;
        _options = options.Value;
    }

    public bool Enabled => _options.AutoInvoiceShipments;

    /// <summary>Invoices and issues the shipment when it is billable and not invoiced yet (nothing happens otherwise).</summary>
    public async Task InvoiceAsync(long shipmentId, CancellationToken cancellationToken)
    {
        if (Enabled)
        {
            await _invoices.AutoInvoiceShipmentAsync(shipmentId, cancellationToken);
        }
    }

    /// <summary>
    /// Cancels the single-shipment invoice of the shipment before its price or payer changes. Returns false when it
    /// was not invoiced. Throws when the invoice has payments / credit notes or covers several shipments.
    /// </summary>
    public async Task<bool> ReleaseAsync(long shipmentId, string reason, CancellationToken cancellationToken) =>
        Enabled && await _invoices.ReleaseShipmentInvoiceAsync(shipmentId, reason, cancellationToken);
}
