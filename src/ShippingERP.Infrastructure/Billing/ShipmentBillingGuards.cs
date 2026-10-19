using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Billing;

/// <summary>Rules that protect invoiced shipments (used by the shipment and pricing services).</summary>
public static class ShipmentBillingGuards
{
    /// <summary>
    /// 422 when the shipment's charge is on an active invoice (draft or issued), or (step 6) is deducted from its
    /// COD amount in a sender settlement. Its price cannot change and it cannot be cancelled until the invoice is
    /// cancelled (or credited and re-billed).
    /// </summary>
    public static async Task EnsureNotInvoicedAsync(AppDbContext dbContext, long shipmentId, string action, CancellationToken cancellationToken)
    {
        var invoice = await dbContext.InvoiceLines
            .AsNoTracking()
            .Where(x => x.ShipmentId == shipmentId && x.IsActive)
            .Select(x => new { x.InvoiceId, x.Invoice.InvoiceNumber })
            .FirstOrDefaultAsync(cancellationToken);

        if (invoice is not null)
        {
            var document = invoice.InvoiceNumber is null ? $"مسودة الفاتورة رقم {invoice.InvoiceId}" : $"الفاتورة {invoice.InvoiceNumber}";
            throw new BusinessRuleException(
                $"لا يمكن {action}: الشحنة مفوترة في {document}. ألغِ الفاتورة (إن لم تُدفع) أو أصدر إشعاراً دائناً أولاً.");
        }

        // Step 6: the charge is collected through the COD (deducted from the sender's money).
        var deducted = await dbContext.CODTransactions
            .AsNoTracking()
            .AnyAsync(x => x.ShipmentId == shipmentId && x.ShippingChargeDeducted, cancellationToken);

        if (deducted)
        {
            throw new BusinessRuleException(
                $"لا يمكن {action}: أجور الشحنة تُخصم من مبلغ الدفع عند الاستلام (جاهزة للتسوية أو مسوّاة مع المرسل).");
        }
    }

    /// <summary>Cancels the pending manual price request of a shipment whose price is being replaced.</summary>
    public static async Task CancelPendingOverridesAsync(
        AppDbContext dbContext,
        long shipmentId,
        long? userId,
        DateTime nowUtc,
        string note,
        CancellationToken cancellationToken)
    {
        var pending = await dbContext.ShipmentPriceOverrides
            .Where(x => x.ShipmentId == shipmentId && x.Status == PriceOverrideStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var request in pending)
        {
            request.Status = PriceOverrideStatus.Cancelled;
            request.DecidedAtUtc = nowUtc;
            request.DecidedByUserId = userId;
            request.DecisionNotes = note;
        }
    }
}
