using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Application.Interfaces.Billing;

namespace ShippingERP.Tests.Common;

/// <summary>Helpers shared by the invoicing / payment tests (step 5).</summary>
public static class BillingTestData
{
    /// <summary>Makes the tax rate with this code the default one (demo: EXEMPT 0 %, VAT5, VAT10).</summary>
    public static Task SetDefaultTaxAsync(IdentityTestContext context, string code) =>
        context.QueryAsync(async db =>
        {
            foreach (var tax in await db.TaxRates.ToListAsync())
            {
                tax.IsDefault = tax.Code == code;
            }

            return await db.SaveChangesAsync();
        });

    public static Task<long> PaymentMethodIdAsync(IdentityTestContext context, string code) =>
        context.QueryAsync(db => db.PaymentMethods.Where(x => x.Code == code).Select(x => x.Id).SingleAsync());

    public static Task<InvoiceDto> InvoiceShipmentAsync(IdentityTestContext context, long shipmentId, bool issue = false, int? paymentTermsDays = null) =>
        context.UseAsync<IInvoiceService, InvoiceDto>(s => s.CreateFromShipmentAsync(
            shipmentId,
            new CreateInvoiceFromShipmentRequest { IssueImmediately = issue, PaymentTermsDays = paymentTermsDays },
            CancellationToken.None));

    public static Task<InvoiceDto> IssueAsync(IdentityTestContext context, long invoiceId) =>
        context.UseAsync<IInvoiceService, InvoiceDto>(s => s.IssueAsync(invoiceId, new IssueInvoiceRequest(), CancellationToken.None));

    public static Task<InvoiceDto> GetInvoiceAsync(IdentityTestContext context, long invoiceId) =>
        context.UseAsync<IInvoiceService, InvoiceDto>(s => s.GetByIdAsync(invoiceId, CancellationToken.None));

    public static async Task<CustomerPaymentCreationResult> PayAsync(
        IdentityTestContext context,
        long customerId,
        decimal amount,
        string methodCode = "CASH",
        string? idempotencyKey = null,
        bool autoAllocate = false,
        (long InvoiceId, decimal Amount)[]? allocations = null)
    {
        var methodId = await PaymentMethodIdAsync(context, methodCode);
        var request = new CreateCustomerPaymentRequest
        {
            CustomerId = customerId,
            PaymentMethodId = methodId,
            Amount = amount,
            AutoAllocate = autoAllocate,
            ReferenceNumber = methodCode == "CASH" ? null : "TRX-1001",
            Allocations = (allocations ?? Array.Empty<(long InvoiceId, decimal Amount)>()).Select(x => new PaymentAllocationRequest { InvoiceId = x.InvoiceId, Amount = x.Amount }).ToList()
        };

        return await context.UseAsync<ICustomerPaymentService, CustomerPaymentCreationResult>(s =>
            s.CreateAsync(request, idempotencyKey, CancellationToken.None));
    }
}
