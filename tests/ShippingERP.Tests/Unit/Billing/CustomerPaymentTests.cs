using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Application.Interfaces.Billing;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Billing;

/// <summary>Customer payments: partial payments, allocation rules, auto-allocation, idempotency, payment methods.</summary>
public sealed class CustomerPaymentTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        await _context.SeedDemoDataAsync();
        _ids = await ShipmentTestData.LoadIdsAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    /// <summary>An issued invoice of 15,000 (standard 2.5 kg) for CUS-00003.</summary>
    private async Task<InvoiceDto> IssuedInvoiceAsync(decimal weightKg = 2.5m, int? paymentTermsDays = null)
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, weightKg: weightKg));
        return await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true, paymentTermsDays: paymentTermsDays);
    }

    [Fact]
    public async Task PartialPayments_MoveTheInvoiceToPartiallyPaid_ThenToPaid()
    {
        var invoice = await IssuedInvoiceAsync();

        var first = await BillingTestData.PayAsync(_context, invoice.CustomerId, 6000m, allocations: new[] { (invoice.Id, 6000m) });
        Assert.StartsWith("PAY-", first.Payment.PaymentNumber);
        Assert.Equal(6000m, first.Payment.AllocatedAmount);
        var afterFirst = await BillingTestData.GetInvoiceAsync(_context, invoice.Id);
        Assert.Equal(InvoiceStatus.PartiallyPaid, afterFirst.Status);
        Assert.Equal(9000m, afterFirst.BalanceDue);

        await BillingTestData.PayAsync(_context, invoice.CustomerId, 9000m, allocations: new[] { (invoice.Id, 9000m) });
        var afterSecond = await BillingTestData.GetInvoiceAsync(_context, invoice.Id);
        Assert.Equal(InvoiceStatus.Paid, afterSecond.Status);
        Assert.Equal(0m, afterSecond.BalanceDue);
        Assert.NotNull(afterSecond.PaidAtUtc);
        Assert.Equal(2, afterSecond.Payments.Count);

        // Sub-ledger: invoice debit 15,000 and two credits; the customer owes nothing.
        var ledger = await _context.QueryAsync(db => db.CustomerAccountTransactions.ToListAsync());
        Assert.Equal(3, ledger.Count);
        Assert.Equal(0m, ledger.Sum(x => x.Debit - x.Credit));
        Assert.Equal(2, await _context.QueryAsync(db => db.OutboxMessages.CountAsync(x => x.EventType == "CustomerPaymentReceived")));
    }

    [Fact]
    public async Task OnePayment_CanPaySeveralInvoices_AndKeepTheRestAsUnallocated()
    {
        var a = await IssuedInvoiceAsync();
        var b = await IssuedInvoiceAsync(weightKg: 9m); // 23,000

        var result = await BillingTestData.PayAsync(_context, a.CustomerId, 40000m, allocations: new[] { (a.Id, 15000m), (b.Id, 20000m) });

        Assert.Equal(35000m, result.Payment.AllocatedAmount);
        Assert.Equal(5000m, result.Payment.UnallocatedAmount);
        Assert.Equal(2, result.Payment.Allocations.Count);
        Assert.Equal(InvoiceStatus.Paid, (await BillingTestData.GetInvoiceAsync(_context, a.Id)).Status);
        Assert.Equal(3000m, (await BillingTestData.GetInvoiceAsync(_context, b.Id)).BalanceDue);
    }

    [Fact]
    public async Task Allocation_AboveTheInvoiceBalance_IsRejected_AndNothingIsSaved()
    {
        var invoice = await IssuedInvoiceAsync();

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            BillingTestData.PayAsync(_context, invoice.CustomerId, 20000m, allocations: new[] { (invoice.Id, 16000m) }));

        Assert.True(exception.Errors.ContainsKey("allocations[0].amount"));
        Assert.Equal(0, await _context.QueryAsync(db => db.CustomerPayments.CountAsync()));
    }

    [Fact]
    public async Task Allocation_ToADraftOrToAnotherCustomersInvoice_IsRejected()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));
        var draft = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);
        var otherCustomer = _ids.Customer("CUS-00007");

        var draftError = await Assert.ThrowsAsync<RequestValidationException>(() =>
            BillingTestData.PayAsync(_context, draft.CustomerId, 1000m, allocations: new[] { (draft.Id, 1000m) }));
        var customerError = await Assert.ThrowsAsync<RequestValidationException>(() =>
            BillingTestData.PayAsync(_context, otherCustomer, 1000m, allocations: new[] { (draft.Id, 1000m) }));

        Assert.True(draftError.Errors.ContainsKey("allocations[0].invoiceId"));
        Assert.True(customerError.Errors.ContainsKey("allocations[0].invoiceId"));
    }

    [Fact]
    public async Task AutoAllocate_PaysTheInvoiceThatIsDueFirst()
    {
        var later = await IssuedInvoiceAsync(paymentTermsDays: 30);
        var sooner = await IssuedInvoiceAsync(paymentTermsDays: 5);

        var result = await BillingTestData.PayAsync(_context, later.CustomerId, 20000m, autoAllocate: true);

        Assert.Equal(20000m, result.Payment.AllocatedAmount);
        Assert.Equal(InvoiceStatus.Paid, (await BillingTestData.GetInvoiceAsync(_context, sooner.Id)).Status);
        Assert.Equal(10000m, (await BillingTestData.GetInvoiceAsync(_context, later.Id)).BalanceDue);
    }

    [Fact]
    public async Task UnallocatedMoney_CanBeAllocatedLater()
    {
        var advance = await BillingTestData.PayAsync(_context, _ids.Customer("CUS-00003"), 10000m);
        Assert.Equal(10000m, advance.Payment.UnallocatedAmount);

        var invoice = await IssuedInvoiceAsync();
        var allocated = await _context.UseAsync<ICustomerPaymentService, CustomerPaymentDto>(s => s.AllocateAsync(
            advance.Payment.Id, new AllocatePaymentRequest { AutoAllocate = true }, CancellationToken.None));

        Assert.Equal(0m, allocated.UnallocatedAmount);
        Assert.Equal(5000m, (await BillingTestData.GetInvoiceAsync(_context, invoice.Id)).BalanceDue);
        Assert.Contains(await _context.QueryAsync(db => db.OutboxMessages.Select(x => x.EventType).ToListAsync()), x => x == "CustomerPaymentAllocated");

        // Allocating does not move money: the ledger still has one invoice debit and one payment credit.
        Assert.Equal(2, await _context.QueryAsync(db => db.CustomerAccountTransactions.CountAsync()));
    }

    [Fact]
    public async Task SameIdempotencyKey_ReturnsTheFirstPayment_InsteadOfRecordingTheMoneyTwice()
    {
        var invoice = await IssuedInvoiceAsync();
        var key = Guid.NewGuid().ToString();

        var first = await BillingTestData.PayAsync(_context, invoice.CustomerId, 5000m, idempotencyKey: key, allocations: new[] { (invoice.Id, 5000m) });
        var retry = await BillingTestData.PayAsync(_context, invoice.CustomerId, 5000m, idempotencyKey: key, allocations: new[] { (invoice.Id, 5000m) });

        Assert.False(first.IsReplay);
        Assert.True(retry.IsReplay);
        Assert.Equal(first.Payment.Id, retry.Payment.Id);
        Assert.Equal(1, await _context.QueryAsync(db => db.CustomerPayments.CountAsync()));
        Assert.Equal(10000m, (await BillingTestData.GetInvoiceAsync(_context, invoice.Id)).BalanceDue);

        // The same key with another body is a client error.
        await Assert.ThrowsAsync<ConflictException>(() =>
            BillingTestData.PayAsync(_context, invoice.CustomerId, 6000m, idempotencyKey: key, allocations: new[] { (invoice.Id, 6000m) }));
    }

    [Fact]
    public async Task CashOnDeliveryMethod_IsNotACustomerPayment_AndBankTransferNeedsAReference()
    {
        var customerId = _ids.Customer("CUS-00003");

        var cod = await Assert.ThrowsAsync<RequestValidationException>(() => BillingTestData.PayAsync(_context, customerId, 1000m, methodCode: "COD"));
        Assert.True(cod.Errors.ContainsKey("paymentMethodId"));

        var bankId = await BillingTestData.PaymentMethodIdAsync(_context, "BANK");
        var bank = await Assert.ThrowsAsync<RequestValidationException>(() => _context.UseAsync<ICustomerPaymentService, CustomerPaymentCreationResult>(s =>
            s.CreateAsync(new CreateCustomerPaymentRequest { CustomerId = customerId, PaymentMethodId = bankId, Amount = 1000m }, null, CancellationToken.None)));
        Assert.True(bank.Errors.ContainsKey("referenceNumber"));
    }

    [Fact]
    public async Task BranchUsers_OnlySeeThePaymentsOfTheirBranch()
    {
        var invoice = await IssuedInvoiceAsync();
        var payment = await BillingTestData.PayAsync(_context, invoice.CustomerId, 1000m, allocations: new[] { (invoice.Id, 1000m) });

        _context.CurrentUser.SignInAs(30, _ids.Aleppo, Permissions.Payments.View);
        await Assert.ThrowsAsync<NotFoundException>(() => _context.UseAsync<ICustomerPaymentService, CustomerPaymentDto>(s =>
            s.GetByIdAsync(payment.Payment.Id, CancellationToken.None)));
        var list = await _context.UseAsync<ICustomerPaymentService, Application.Common.Models.PagedResult<CustomerPaymentListItemDto>>(s =>
            s.GetPagedAsync(new CustomerPaymentListQuery(), CancellationToken.None));
        Assert.Equal(0, list.TotalCount);

        _context.CurrentUser.SignInAs(31, _ids.Damascus, Permissions.Payments.View);
        Assert.Equal(payment.Payment.Id, (await _context.UseAsync<ICustomerPaymentService, CustomerPaymentDto>(s =>
            s.GetByIdAsync(payment.Payment.Id, CancellationToken.None))).Id);
    }

    [Fact]
    public async Task Receipt_IsAPdf_WithTheLogoWhenThereIsOne()
    {
        // A tiny valid PNG as the company logo (the profile is created by the demo data).
        await _context.QueryAsync(async db =>
        {
            var profile = await db.CompanyProfiles.FirstAsync();
            profile.LogoImage = ShipmentTestData.PngBytes;
            profile.LogoContentType = "image/png";
            return await db.SaveChangesAsync();
        });

        var invoice = await IssuedInvoiceAsync();
        var payment = await BillingTestData.PayAsync(_context, invoice.CustomerId, 6000m, allocations: new[] { (invoice.Id, 6000m) });

        var pdf = await _context.UseAsync<ICustomerPaymentService, ShippingERP.Application.Common.Files.FileDownload>(
            s => s.GetReceiptPdfAsync(payment.Payment.Id, CancellationToken.None));

        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.Equal($"{payment.Payment.PaymentNumber}.pdf", pdf.FileName);
        var header = new byte[4];
        await using var stream = pdf.Content;
        Assert.Equal(4, await stream.ReadAsync(header));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(header));
    }
}

