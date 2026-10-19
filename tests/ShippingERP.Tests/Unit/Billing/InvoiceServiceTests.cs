using System.Text;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Files;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Billing;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Billing;

/// <summary>
/// Invoices and credit notes: drafts, issuing, duplicate prevention, consolidation, cancellation, credit notes,
/// overdue status, PDF and Excel. Demo standard shipment (2.5 kg) = 15,000 SYP before tax.
/// </summary>
public sealed class InvoiceServiceTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        await _context.SeedDemoDataAsync();
        _ids = await ShipmentTestData.LoadIdsAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<T> Use<T>(Func<IInvoiceService, Task<T>> action) => _context.UseAsync(action);

    private async Task<ShipmentDto> NewShipmentAsync(decimal weightKg = 2.5m, string sender = "CUS-00003") =>
        await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, sender: sender, weightKg: weightKg));

    [Fact]
    public async Task FromShipment_CreatesADraft_ThenIssueGivesTheNumber_DebitsTheAccount_AndPublishesTheEvent()
    {
        await BillingTestData.SetDefaultTaxAsync(_context, "VAT5");
        var shipment = await NewShipmentAsync();

        var draft = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);

        Assert.Equal(InvoiceStatus.Draft, draft.Status);
        Assert.Null(draft.InvoiceNumber);
        var line = Assert.Single(draft.Lines);
        Assert.Equal(shipment.Id, line.ShipmentId);
        Assert.Equal(15000m, line.TaxableAmount);
        Assert.Equal(750m, line.TaxAmount);
        Assert.Equal(15750m, draft.TotalAmount);
        Assert.Equal(15750m, draft.BalanceDue);
        Assert.Equal(0, await _context.QueryAsync(db => db.CustomerAccountTransactions.CountAsync()));

        var issued = await BillingTestData.IssueAsync(_context, draft.Id);

        Assert.Equal(InvoiceStatus.Issued, issued.Status);
        Assert.Equal($"INV-{_context.Clock.UtcNow.Year}-000001", issued.InvoiceNumber);
        Assert.Equal(issued.InvoiceDateUtc!.Value.AddDays(30), issued.DueDateUtc);

        var ledger = await _context.QueryAsync(db => db.CustomerAccountTransactions.ToListAsync());
        var debit = Assert.Single(ledger);
        Assert.Equal(CustomerAccountTransactionType.InvoiceIssued, debit.TransactionType);
        Assert.Equal(15750m, debit.Debit);
        Assert.Equal(issued.InvoiceNumber, debit.DocumentNumber);

        var outbox = Assert.Single(await _context.QueryAsync(db => db.OutboxMessages.ToListAsync()));
        Assert.Equal("InvoiceIssued", outbox.EventType);
        Assert.Contains(issued.InvoiceNumber!, outbox.PayloadJson);
        Assert.Null(outbox.ProcessedAtUtc);
    }

    [Fact]
    public async Task Shipment_CannotBeInvoicedTwice_UntilTheFirstInvoiceIsCancelled()
    {
        var shipment = await NewShipmentAsync();
        var first = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);

        await Assert.ThrowsAsync<ConflictException>(() => BillingTestData.InvoiceShipmentAsync(_context, shipment.Id));
        await Assert.ThrowsAsync<ConflictException>(() => Use(s => s.CreateConsolidatedAsync(
            new CreateConsolidatedInvoiceRequest { CustomerId = shipment.SenderCustomerId, ShipmentIds = new List<long> { shipment.Id } },
            CancellationToken.None)));

        await Use(s => s.CancelAsync(first.Id, new CancelInvoiceRequest { Reason = "أُنشئت بالخطأ" }, CancellationToken.None));

        var second = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(1, await _context.QueryAsync(db => db.InvoiceLines.CountAsync(x => x.ShipmentId == shipment.Id && x.IsActive)));
    }

    [Fact]
    public async Task Consolidated_BillsSeveralShipmentsOfTheSameSender_InOneInvoice()
    {
        var a = await NewShipmentAsync();
        var b = await NewShipmentAsync(weightKg: 9m);

        var invoice = await Use(s => s.CreateConsolidatedAsync(
            new CreateConsolidatedInvoiceRequest { CustomerId = a.SenderCustomerId, ShipmentIds = new List<long> { a.Id, b.Id }, IssueImmediately = true },
            CancellationToken.None));

        Assert.True(invoice.IsConsolidated);
        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
        Assert.Equal(2, invoice.Lines.Count);
        Assert.Equal(38000m, invoice.TotalAmount); // 15,000 + 23,000
    }

    [Fact]
    public async Task Consolidated_WithAShipmentOfAnotherSender_IsRefused()
    {
        var mine = await NewShipmentAsync();
        var other = await NewShipmentAsync(sender: "CUS-00007");

        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.CreateConsolidatedAsync(
            new CreateConsolidatedInvoiceRequest { CustomerId = mine.SenderCustomerId, ShipmentIds = new List<long> { mine.Id, other.Id } },
            CancellationToken.None)));
    }

    [Fact]
    public async Task ChargesOnTheRecipient_AreInvoicedToTheRecipient_NotToTheSender()
    {
        var request = ShipmentTestData.NewRequest(_ids, sender: "CUS-00003", recipient: "CUS-00002");
        request.ChargesPaidBy = ChargesPayer.Recipient;
        var shipment = await ShipmentTestData.CreateAsync(_context, request);
        Assert.Equal(ChargesPayer.Recipient, shipment.ChargesPaidBy);

        // The sender cannot be invoiced for it...
        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.CreateConsolidatedAsync(
            new CreateConsolidatedInvoiceRequest { CustomerId = shipment.SenderCustomerId, ShipmentIds = new List<long> { shipment.Id } },
            CancellationToken.None)));

        // ...the invoice from the shipment goes to the recipient.
        var draft = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);
        Assert.Equal(shipment.RecipientCustomerId, draft.CustomerId);
        Assert.Equal(15000m, Assert.Single(draft.Lines).TaxableAmount);
    }

    [Fact]
    public async Task UnitPrice_TypedOnTheShipment_IsWeightOrVolumeTimesThePrice_InTheChosenCurrency()
    {
        var usd = await _context.QueryAsync(db => db.Currencies.Where(x => x.Code == "USD").Select(x => x.Id).SingleAsync());

        var byWeight = ShipmentTestData.NewRequest(_ids, weightKg: 12.5m);
        byWeight.UnitPrice = 4m;
        var weightShipment = await ShipmentTestData.CreateAsync(_context, byWeight);
        Assert.Equal(50m, weightShipment.TotalCharge);
        Assert.Equal(4m, weightShipment.UnitPrice);

        var byVolume = ShipmentTestData.NewRequest(_ids, weightKg: 40m);
        byVolume.PricingBasis = PricingBasis.Volume;
        byVolume.TotalVolumeCbm = 1.75m;
        byVolume.UnitPrice = 95m;
        byVolume.CurrencyId = usd;
        var volumeShipment = await ShipmentTestData.CreateAsync(_context, byVolume);
        Assert.Equal(166.25m, volumeShipment.TotalCharge);
        Assert.Equal("USD", volumeShipment.CurrencyCode);
        Assert.Equal(1.75m, volumeShipment.TotalVolumeCbm);
    }

    [Fact]
    public async Task Consolidated_ByPeriod_TakesEveryShipmentThatIsNotInvoicedYet()
    {
        var from = _context.Clock.UtcNow.AddMinutes(-1);
        var invoiced = await NewShipmentAsync();
        await BillingTestData.InvoiceShipmentAsync(_context, invoiced.Id);
        await NewShipmentAsync();
        await NewShipmentAsync(weightKg: 7m);

        var eligible = await Use(s => s.GetEligibleShipmentsAsync(
            new EligibleShipmentsQuery { CustomerId = invoiced.SenderCustomerId }, CancellationToken.None));
        Assert.Equal(2, eligible.TotalCount);

        var invoice = await Use(s => s.CreateConsolidatedAsync(
            new CreateConsolidatedInvoiceRequest { CustomerId = invoiced.SenderCustomerId, FromUtc = from, ToUtc = _context.Clock.UtcNow.AddMinutes(1) },
            CancellationToken.None));

        Assert.Equal(2, invoice.Lines.Count);
        Assert.Equal(34000m, invoice.TotalAmount); // 15,000 + 19,000
    }

    [Fact]
    public async Task DraftInvoiceDiscount_IsSpreadOverTheLines_AndTheTaxIsRecalculated()
    {
        await BillingTestData.SetDefaultTaxAsync(_context, "VAT5");
        var a = await NewShipmentAsync();            // 15,000 + 750 tax
        var b = await NewShipmentAsync(weightKg: 9m); // 23,000 + 1,150 tax
        var draft = await Use(s => s.CreateConsolidatedAsync(
            new CreateConsolidatedInvoiceRequest { CustomerId = a.SenderCustomerId, ShipmentIds = new List<long> { a.Id, b.Id } },
            CancellationToken.None));
        Assert.Equal(39900m, draft.TotalAmount);

        var updated = await Use(s => s.UpdateDraftAsync(
            draft.Id,
            new UpdateDraftInvoiceRequest { PaymentTermsDays = 15, InvoiceDiscountAmount = 3800m, Notes = "خصم نهاية الشهر" },
            CancellationToken.None));

        // 10 % of each line: 1,500 and 2,300; tax 5 % of 13,500 and 20,700.
        Assert.Equal(new[] { 1500m, 2300m }, updated.Lines.OrderBy(x => x.LineNumber).Select(x => x.InvoiceDiscountAmount).ToArray());
        Assert.Equal(3800m, updated.InvoiceDiscountAmount);
        Assert.Equal(34200m, updated.TaxableAmount);
        Assert.Equal(1710m, updated.TaxAmount);
        Assert.Equal(35910m, updated.TotalAmount);
        Assert.Equal(15, updated.PaymentTermsDays);
    }

    [Fact]
    public async Task IssuedInvoice_CannotBeEdited_OrIssuedAgain()
    {
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.UpdateDraftAsync(
            invoice.Id, new UpdateDraftInvoiceRequest { PaymentTermsDays = 10 }, CancellationToken.None)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => BillingTestData.IssueAsync(_context, invoice.Id));
    }

    [Fact]
    public async Task CancellingAnIssuedInvoice_ReversesTheDebit_AndFreesTheShipment()
    {
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);

        var cancelled = await Use(s => s.CancelAsync(invoice.Id, new CancelInvoiceRequest { Reason = "عنوان الفوترة خاطئ" }, CancellationToken.None));

        Assert.Equal(InvoiceStatus.Cancelled, cancelled.Status);
        Assert.Equal(0m, cancelled.BalanceDue);
        var ledger = await _context.QueryAsync(db => db.CustomerAccountTransactions.OrderBy(x => x.Id).ToListAsync());
        Assert.Equal(new[] { CustomerAccountTransactionType.InvoiceIssued, CustomerAccountTransactionType.InvoiceCancelled }, ledger.Select(x => x.TransactionType).ToArray());
        Assert.Equal(0m, ledger.Sum(x => x.Debit - x.Credit));
        Assert.Contains(await _context.QueryAsync(db => db.OutboxMessages.Select(x => x.EventType).ToListAsync()), x => x == "InvoiceCancelled");

        var again = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);
        Assert.Equal(InvoiceStatus.Draft, again.Status);
    }

    [Fact]
    public async Task CancellingAnInvoiceWithAPayment_IsRefused()
    {
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);
        await BillingTestData.PayAsync(_context, invoice.CustomerId, 1000m, allocations: new[] { (invoice.Id, 1000m) });

        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.CancelAsync(
            invoice.Id, new CancelInvoiceRequest { Reason = "محاولة إلغاء" }, CancellationToken.None)));
    }

    [Fact]
    public async Task CancellingAnInvoicedShipment_IsRefused()
    {
        var shipment = await NewShipmentAsync();
        await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.UseAsync<IShipmentService, ShipmentDto>(s =>
            s.CancelAsync(shipment.Id, new CancelShipmentRequest { Reason = "طلب العميل" }, CancellationToken.None)));
    }

    [Fact]
    public async Task PartialCreditNote_ReversesTheTaxAtTheLineRate_AndReducesTheBalance()
    {
        await BillingTestData.SetDefaultTaxAsync(_context, "VAT5");
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);
        var line = Assert.Single(invoice.Lines);

        var creditNote = await Use(s => s.CreateCreditNoteAsync(invoice.Id, new CreateCreditNoteRequest
        {
            Reason = "تأخر التسليم",
            Lines = new List<CreditNoteLineRequest> { new() { InvoiceLineId = line.Id, TaxableAmount = 5000m } }
        }, CancellationToken.None));

        Assert.Equal($"CRN-{_context.Clock.UtcNow.Year}-000001", creditNote.CreditNoteNumber);
        Assert.Equal(5000m, creditNote.TaxableAmount);
        Assert.Equal(250m, creditNote.TaxAmount);
        Assert.Equal(5250m, creditNote.TotalAmount);
        Assert.Equal(5250m, creditNote.AppliedToInvoiceAmount);

        var after = await BillingTestData.GetInvoiceAsync(_context, invoice.Id);
        Assert.Equal(InvoiceStatus.PartiallyPaid, after.Status);
        Assert.Equal(10500m, after.BalanceDue);
        Assert.Equal(10000m, Assert.Single(after.Lines).CreditableAmount);
        Assert.Single(after.CreditNotes);
    }

    [Fact]
    public async Task CreditAll_MakesTheInvoiceCredited_AndTheShipmentCanBeBilledAgain()
    {
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);

        await Use(s => s.CreateCreditNoteAsync(invoice.Id, new CreateCreditNoteRequest { Reason = "الشحنة لم تُنفذ", CreditAll = true }, CancellationToken.None));

        var after = await BillingTestData.GetInvoiceAsync(_context, invoice.Id);
        Assert.Equal(InvoiceStatus.Credited, after.Status);
        Assert.Equal(0m, after.BalanceDue);
        Assert.Equal(0m, await _context.QueryAsync(db => db.CustomerAccountTransactions.SumAsync(x => x.Debit - x.Credit)));

        // Nothing left to credit.
        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.CreateCreditNoteAsync(
            invoice.Id, new CreateCreditNoteRequest { Reason = "مرة ثانية", CreditAll = true }, CancellationToken.None)));

        var rebilled = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);
        Assert.Equal(InvoiceStatus.Draft, rebilled.Status);
    }

    [Fact]
    public async Task CreditNote_LargerThanWhatIsLeftOnTheLine_IsRejected()
    {
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);
        var lineId = Assert.Single(invoice.Lines).Id;

        await Use(s => s.CreateCreditNoteAsync(invoice.Id, new CreateCreditNoteRequest
        {
            Reason = "خصم أول",
            Lines = new List<CreditNoteLineRequest> { new() { InvoiceLineId = lineId, TaxableAmount = 5000m } }
        }, CancellationToken.None));

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => Use(s => s.CreateCreditNoteAsync(invoice.Id, new CreateCreditNoteRequest
        {
            Reason = "خصم ثانٍ",
            Lines = new List<CreditNoteLineRequest> { new() { InvoiceLineId = lineId, TaxableAmount = 10001m } }
        }, CancellationToken.None)));

        Assert.True(exception.Errors.ContainsKey("lines[0].taxableAmount"));
    }

    [Fact]
    public async Task CreditNote_OnAPaidInvoice_BecomesUnappliedCustomerCredit()
    {
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);
        await BillingTestData.PayAsync(_context, invoice.CustomerId, 15000m, allocations: new[] { (invoice.Id, 15000m) });

        var creditNote = await Use(s => s.CreateCreditNoteAsync(invoice.Id, new CreateCreditNoteRequest
        {
            Reason = "تعويض عن تأخير",
            Lines = new List<CreditNoteLineRequest> { new() { InvoiceLineId = invoice.Lines[0].Id, TaxableAmount = 2000m } }
        }, CancellationToken.None));

        Assert.Equal(0m, creditNote.AppliedToInvoiceAmount);
        Assert.Equal(2000m, creditNote.UnappliedAmount);
        Assert.Equal(InvoiceStatus.Paid, (await BillingTestData.GetInvoiceAsync(_context, invoice.Id)).Status);

        // The customer is now in credit by 2,000 (we owe him).
        Assert.Equal(-2000m, await _context.QueryAsync(db => db.CustomerAccountTransactions.SumAsync(x => x.Debit - x.Credit)));
    }

    [Fact]
    public async Task CreditNote_OnADraft_IsRefused()
    {
        var shipment = await NewShipmentAsync();
        var draft = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.CreateCreditNoteAsync(
            draft.Id, new CreateCreditNoteRequest { Reason = "مسودة", CreditAll = true }, CancellationToken.None)));
    }

    [Fact]
    public async Task OverdueJob_MarksOpenInvoicesPastTheirDueDate()
    {
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true, paymentTermsDays: 10);

        Assert.Equal(0, await Use(s => s.RefreshOverdueAsync(CancellationToken.None)));

        _context.Clock.Advance(TimeSpan.FromDays(11));
        Assert.Equal(1, await Use(s => s.RefreshOverdueAsync(CancellationToken.None)));
        Assert.Equal(InvoiceStatus.Overdue, (await BillingTestData.GetInvoiceAsync(_context, invoice.Id)).Status);

        var overdue = await Use(s => s.GetPagedAsync(new InvoiceListQuery { Status = InvoiceStatus.Overdue }, CancellationToken.None));
        Assert.Equal(1, overdue.TotalCount);
    }

    [Fact]
    public async Task InvoiceList_SupportsFiltersSearchAndSorting()
    {
        var a = await NewShipmentAsync();
        var b = await NewShipmentAsync(weightKg: 9m);
        var issued = await BillingTestData.InvoiceShipmentAsync(_context, a.Id, issue: true);
        await BillingTestData.InvoiceShipmentAsync(_context, b.Id);

        var drafts = await Use(s => s.GetPagedAsync(new InvoiceListQuery { Status = InvoiceStatus.Draft }, CancellationToken.None));
        var bySearch = await Use(s => s.GetPagedAsync(new InvoiceListQuery { Search = issued.InvoiceNumber }, CancellationToken.None));
        var byAmount = await Use(s => s.GetPagedAsync(new InvoiceListQuery { SortBy = "totalAmount", SortDirection = "desc" }, CancellationToken.None));
        var withBalance = await Use(s => s.GetPagedAsync(new InvoiceListQuery { HasBalance = true }, CancellationToken.None));
        var byShipment = await Use(s => s.GetPagedAsync(new InvoiceListQuery { ShipmentId = b.Id }, CancellationToken.None));

        Assert.Equal(1, drafts.TotalCount);
        Assert.Equal(issued.Id, Assert.Single(bySearch.Items).Id);
        Assert.Equal(new[] { 23000m, 15000m }, byAmount.Items.Select(x => x.TotalAmount).ToArray());
        Assert.Equal(issued.Id, Assert.Single(withBalance.Items).Id);
        Assert.Equal(InvoiceStatus.Draft, Assert.Single(byShipment.Items).Status);
    }

    [Fact]
    public async Task Pdf_AndExcelExport_AreGenerated()
    {
        var shipment = await NewShipmentAsync();
        var invoice = await BillingTestData.InvoiceShipmentAsync(_context, shipment.Id, issue: true);

        var pdf = await Use(s => s.GetPdfAsync(invoice.Id, CancellationToken.None));
        var excel = await Use(s => s.ExportAsync(new InvoiceListQuery(), CancellationToken.None));

        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.Equal($"{invoice.InvoiceNumber}.pdf", pdf.FileName);
        Assert.Equal("%PDF", await Header(pdf, 4));
        Assert.EndsWith(".xlsx", excel.FileName);
        Assert.Equal("PK", await Header(excel, 2));
    }

    private static async Task<string> Header(FileDownload file, int length)
    {
        await using var stream = file.Content;
        var buffer = new byte[length];
        var read = await stream.ReadAsync(buffer);
        return Encoding.ASCII.GetString(buffer, 0, read);
    }
}
