using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Billing;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Billing;

/// <summary>
/// Step 9: with Billing:AutoInvoiceShipments the shipment total is the debt of the customer who pays the charges
/// at once (an issued invoice made by the system), a payment reduces it, and editing / cancelling the shipment
/// follows. Demo standard shipment: 2.5 kg = 15,000 SYP, 9 kg = 23,000 SYP.
/// </summary>
public sealed class AutoBillingTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new(autoInvoiceShipments: true);
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        await _context.SeedDemoDataAsync();
        _ids = await ShipmentTestData.LoadIdsAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private async Task<decimal> DebtAsync(long customerId)
    {
        var balances = await _context.UseAsync<IReceivablesReportService, IReadOnlyList<CustomerBalanceDto>>(
            s => s.GetBalancesAsync(customerId, CancellationToken.None));
        return balances.Where(x => x.CurrencyCode == "SYP").Sum(x => x.Balance);
    }

    private Task<ShipmentDto> UpdateAsync(long id, SaveShipmentRequest request) =>
        _context.UseAsync<IShipmentService, ShipmentDto>(s => s.UpdateAsync(id, request, CancellationToken.None));

    [Fact]
    public async Task NewShipment_IsTheDebtOfThePayer_AndAPaymentReducesIt()
    {
        var request = ShipmentTestData.NewRequest(_ids, sender: "CUS-00003", recipient: "CUS-00002");
        request.ChargesPaidBy = ChargesPayer.Recipient;

        var shipment = await ShipmentTestData.CreateAsync(_context, request);

        Assert.NotNull(shipment.InvoiceId);
        var invoice = await BillingTestData.GetInvoiceAsync(_context, shipment.InvoiceId!.Value);
        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
        Assert.Equal(shipment.RecipientCustomerId, invoice.CustomerId);
        Assert.Equal(15000m, invoice.TotalAmount);

        Assert.Equal(15000m, await DebtAsync(shipment.RecipientCustomerId));
        Assert.Equal(0m, await DebtAsync(shipment.SenderCustomerId));

        await BillingTestData.PayAsync(_context, shipment.RecipientCustomerId, 10000m, autoAllocate: true);

        Assert.Equal(5000m, await DebtAsync(shipment.RecipientCustomerId));
    }

    [Fact]
    public async Task EditingThePrice_IssuesTheInvoiceAgain_UntilAPaymentIsRecorded()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));
        var firstInvoiceId = shipment.InvoiceId!.Value;

        var edited = await UpdateAsync(shipment.Id, ShipmentTestData.NewRequest(_ids, weightKg: 9m));

        Assert.NotNull(edited.InvoiceId);
        Assert.NotEqual(firstInvoiceId, edited.InvoiceId);
        Assert.Equal(InvoiceStatus.Cancelled, (await BillingTestData.GetInvoiceAsync(_context, firstInvoiceId)).Status);
        Assert.Equal(23000m, await DebtAsync(shipment.SenderCustomerId));

        await BillingTestData.PayAsync(_context, shipment.SenderCustomerId, 1000m, autoAllocate: true);

        await Assert.ThrowsAsync<BusinessRuleException>(() => UpdateAsync(shipment.Id, ShipmentTestData.NewRequest(_ids, weightKg: 2.5m)));
        Assert.Equal(22000m, await DebtAsync(shipment.SenderCustomerId));
    }

    [Fact]
    public async Task CancellingTheShipment_CancelsItsDebt()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));
        Assert.Equal(15000m, await DebtAsync(shipment.SenderCustomerId));

        await _context.UseAsync<IShipmentService, ShipmentDto>(s =>
            s.CancelAsync(shipment.Id, new CancelShipmentRequest { Reason = "طلب العميل" }, CancellationToken.None));

        Assert.Equal(0m, await DebtAsync(shipment.SenderCustomerId));
    }
}
