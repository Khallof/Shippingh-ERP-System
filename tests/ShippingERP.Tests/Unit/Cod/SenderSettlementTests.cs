using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Application.Interfaces.Cod;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Cod;

/// <summary>
/// Sender settlements: full cycle with the cashbox, four-eyes approval, partial settlement, reservation, bank
/// payment, insufficient cash, reversal, negative net, statement PDF.
/// </summary>
public sealed class SenderSettlementTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        _ids = await CodTestData.SeedAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<SenderSettlementDto> Use(Func<ISenderSettlementService, Task<SenderSettlementDto>> action) =>
        _context.UseAsync(action);

    [Fact]
    public async Task FullCycle_PaysTheNetFromTheCashbox_AndSettlesTheCod()
    {
        var ready = await CodTestData.ReadyCodAsync(_context, _ids);
        var balanceBefore = await CodTestData.CashboxBalanceAsync(_context);

        var draft = await CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId);
        Assert.StartsWith("SST-", draft.SettlementNumber);
        Assert.Equal(SettlementStatus.Draft, draft.Status);
        Assert.Equal(CodTestData.Net, draft.NetPayable);
        Assert.Equal(CodTestData.Cod, draft.TotalGrossReleased);
        Assert.Equal(15000m, draft.TotalShippingCharges);
        Assert.Equal(3000m, draft.TotalCodCommission);
        Assert.Equal(draft.Id, (await CodTestData.GetCodAsync(_context, ready.Id)).OpenSettlementId);

        await CodTestData.ApproveSettlementAsync(_context, draft.Id);
        var paid = await CodTestData.PayCashAsync(_context, draft.Id);

        Assert.Equal(SettlementStatus.Paid, paid.Status);
        Assert.StartsWith("PMV-", paid.CashPaymentVoucherNumber);
        Assert.Equal(balanceBefore - CodTestData.Net, await CodTestData.CashboxBalanceAsync(_context));

        var cod = await CodTestData.GetCodAsync(_context, ready.Id);
        Assert.Equal(CODStatus.SettledToSender, cod.Status);
        Assert.Equal(CodTestData.Net, cod.SettledAmount);
        Assert.True(cod.DeductionsRecognized);
        Assert.Null(cod.OpenSettlementId);

        // Revenue is recognized only now: shipping 15,000 + commission 3,000; the 100,000 is released from the liability.
        var payload = await _context.QueryAsync(db => db.OutboxMessages.Where(x => x.EventType == "SenderSettlementPaid").Select(x => x.PayloadJson).SingleAsync());
        Assert.Contains("ShippingRevenue", payload);
        Assert.Contains("CodCommissionRevenue", payload);
        Assert.Empty(await CodTestData.UnbalancedPostingsAsync(_context));
    }

    [Fact]
    public async Task Approval_ByTheCreator_IsRefused()
    {
        var ready = await CodTestData.ReadyCodAsync(_context, _ids);
        _context.CurrentUser.SignInAsSystemAdmin(20);
        var draft = await CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CodTestData.ApproveSettlementAsync(_context, draft.Id));

        _context.CurrentUser.SignInAsSystemAdmin(21);
        var approved = await CodTestData.ApproveSettlementAsync(_context, draft.Id);
        Assert.Equal(SettlementStatus.Approved, approved.Status);
        Assert.Equal(21L, approved.ApprovedByUserId);
    }

    [Fact]
    public async Task PayingADraft_IsRefused()
    {
        var ready = await CodTestData.ReadyCodAsync(_context, _ids);
        var draft = await CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CodTestData.PayCashAsync(_context, draft.Id));
    }

    [Fact]
    public async Task PartialSettlement_ThenTheRest_RecognizesTheDeductionsOnce()
    {
        var ready = await CodTestData.ReadyCodAsync(_context, _ids);

        var first = await CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId, new SettlementLineRequest { CodTransactionId = ready.Id, Amount = 50000m });
        await CodTestData.ApproveSettlementAsync(_context, first.Id);
        await CodTestData.PayCashAsync(_context, first.Id);

        var partial = await CodTestData.GetCodAsync(_context, ready.Id);
        Assert.Equal(CODStatus.PartiallySettled, partial.Status);
        Assert.Equal(32000m, partial.RemainingPayable);

        var second = await CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId);
        Assert.Equal(32000m, second.NetPayable);
        Assert.Equal(0m, second.TotalShippingCharges);
        Assert.False(Assert.Single(second.Lines).RecognizesDeductions);
        await CodTestData.ApproveSettlementAsync(_context, second.Id);
        await CodTestData.PayCashAsync(_context, second.Id);

        var settled = await CodTestData.GetCodAsync(_context, ready.Id);
        Assert.Equal(CODStatus.SettledToSender, settled.Status);
        Assert.Equal(CodTestData.Cod, first.TotalGrossReleased + second.TotalGrossReleased);
    }

    [Fact]
    public async Task ACodReservedByAnOpenSettlement_CannotBeAddedToAnother_UntilItIsCancelled()
    {
        var ready = await CodTestData.ReadyCodAsync(_context, _ids);
        var first = await CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId, new SettlementLineRequest { CodTransactionId = ready.Id }));
        Assert.Contains(first.SettlementNumber, error.Message);

        var cancelled = await Use(s => s.CancelAsync(first.Id, new SettlementReasonRequest { Reason = "خطأ في الاختيار" }, CancellationToken.None));
        Assert.Equal(SettlementStatus.Cancelled, cancelled.Status);

        var again = await CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId, new SettlementLineRequest { CodTransactionId = ready.Id });
        Assert.Equal(CodTestData.Net, again.NetPayable);
    }

    [Fact]
    public async Task CodNotReadyYet_CannotBeSettled()
    {
        var collected = await CodTestData.CollectedCodAsync(_context, _ids);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CodTestData.CreateSettlementAsync(_context, collected.SenderCustomerId, new SettlementLineRequest { CodTransactionId = collected.Id }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => CodTestData.CreateSettlementAsync(_context, collected.SenderCustomerId));
    }

    [Fact]
    public async Task BankPayment_NeedsAReference_AndDoesNotTouchTheCashbox()
    {
        var ready = await CodTestData.ReadyCodAsync(_context, _ids);
        var draft = await CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId);
        await CodTestData.ApproveSettlementAsync(_context, draft.Id);
        var bankMethod = await BillingTestData.PaymentMethodIdAsync(_context, "BANK");
        var bankAccount = await CodTestData.BankAccountIdAsync(_context);
        var cashBefore = await CodTestData.CashboxBalanceAsync(_context);

        var missing = await Assert.ThrowsAsync<RequestValidationException>(() => Use(s =>
            s.PayAsync(draft.Id, new PaySenderSettlementRequest { PaymentMethodId = bankMethod, BankAccountId = bankAccount }, CancellationToken.None)));
        Assert.True(missing.Errors.ContainsKey("referenceNumber"));

        var paid = await Use(s => s.PayAsync(draft.Id,
            new PaySenderSettlementRequest { PaymentMethodId = bankMethod, BankAccountId = bankAccount, ReferenceNumber = "TRX-7781" }, CancellationToken.None));

        Assert.Equal(SettlementStatus.Paid, paid.Status);
        Assert.Equal(bankAccount, paid.BankAccountId);
        Assert.Null(paid.CashPaymentVoucherId);
        Assert.Equal(cashBefore, await CodTestData.CashboxBalanceAsync(_context));
    }

    [Fact]
    public async Task CashPayment_LargerThanTheCashboxBalance_IsRefused_AndNothingChanges()
    {
        var ready = await CodTestData.ReadyCodAsync(_context, _ids);
        var draft = await CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId);
        await CodTestData.ApproveSettlementAsync(_context, draft.Id);

        // Spend almost everything in the Damascus cashbox first.
        var damascus = await CodTestData.CashboxIdAsync(_context);
        var balance = await CodTestData.CashboxBalanceAsync(_context);
        await CodTestData.PayExpenseAsync(_context, damascus, balance - 1000m);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CodTestData.PayCashAsync(_context, draft.Id));

        Assert.Equal(SettlementStatus.Approved, (await Use(s => s.GetByIdAsync(draft.Id, CancellationToken.None))).Status);
        Assert.Equal(1000m, await CodTestData.CashboxBalanceAsync(_context));
        Assert.Equal(CODStatus.ReadyForSenderSettlement, (await CodTestData.GetCodAsync(_context, ready.Id)).Status);
    }

    [Fact]
    public async Task Reverse_ReturnsTheMoney_AndMakesTheCodPayableAgain()
    {
        var ready = await CodTestData.ReadyCodAsync(_context, _ids);
        var balanceBefore = await CodTestData.CashboxBalanceAsync(_context);
        var draft = await CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId);
        await CodTestData.ApproveSettlementAsync(_context, draft.Id);
        var paid = await CodTestData.PayCashAsync(_context, draft.Id);

        var reversed = await Use(s => s.ReverseAsync(paid.Id, new SettlementReasonRequest { Reason = "دُفعت للمرسل الخطأ" }, CancellationToken.None));

        Assert.Equal(SettlementStatus.Reversed, reversed.Status);
        Assert.Equal(balanceBefore, await CodTestData.CashboxBalanceAsync(_context));
        var cod = await CodTestData.GetCodAsync(_context, ready.Id);
        Assert.Equal(CODStatus.ReadyForSenderSettlement, cod.Status);
        Assert.Equal(0m, cod.SettledAmount);
        Assert.False(cod.DeductionsRecognized);

        var voucher = await _context.UseAsync<ICashVoucherService, CashPaymentVoucherDto>(s => s.GetPaymentAsync(paid.CashPaymentVoucherId!.Value, CancellationToken.None));
        Assert.Equal(CashVoucherStatus.Cancelled, voucher.Status);
        Assert.Contains("SenderSettlementReversed", await _context.QueryAsync(db => db.OutboxMessages.Select(x => x.EventType).ToListAsync()));
    }

    [Fact]
    public async Task SettlementPaymentVoucher_CannotBeCancelledDirectly()
    {
        var ready = await CodTestData.ReadyCodAsync(_context, _ids);
        var draft = await CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId);
        await CodTestData.ApproveSettlementAsync(_context, draft.Id);
        var paid = await CodTestData.PayCashAsync(_context, draft.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.UseAsync<ICashVoucherService, CashPaymentVoucherDto>(s =>
            s.CancelPaymentAsync(paid.CashPaymentVoucherId!.Value, new CancelCashVoucherRequest { Reason = "محاولة إلغاء مباشرة" }, CancellationToken.None)));
    }

    [Fact]
    public async Task ChargesHigherThanTheCollectedMoney_GiveANegativeNet_ThatCannotBePaidAlone()
    {
        // COD 5,000 on a 2.5 kg shipment: charges 15,000 + 2,050 = 17,050 -> net -12,050.
        var ready = await CodTestData.ReadyCodAsync(_context, _ids, cod: 5000m);
        Assert.Equal(-12050m, ready.NetPayableAmount);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId));

        // Together with a bigger COD of the same sender the total is positive.
        var big = await CodTestData.ReadyCodAsync(_context, _ids);
        var settlement = await CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId);
        Assert.Equal(CodTestData.Net - 12050m, settlement.NetPayable);
        Assert.Equal(2, settlement.Lines.Count);
        Assert.Contains(settlement.Lines, x => x.CodTransactionId == big.Id);
    }

    [Fact]
    public async Task SettlementStatementPdf_IsGenerated()
    {
        var ready = await CodTestData.ReadyCodAsync(_context, _ids);
        var draft = await CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId);

        var file = await _context.UseAsync<ISenderSettlementService, Application.Common.Files.FileDownload>(s => s.GetPdfAsync(draft.Id, CancellationToken.None));

        Assert.Equal("application/pdf", file.ContentType);
        using var memory = new MemoryStream();
        await file.Content.CopyToAsync(memory);
        Assert.True(memory.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(memory.ToArray(), 0, 4));
    }
}
