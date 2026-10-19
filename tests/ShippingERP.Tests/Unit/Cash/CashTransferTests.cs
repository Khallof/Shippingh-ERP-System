using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Cash;

/// <summary>Cash transfers: same branch, another branch (in transit), bank deposit, four-eyes approval, rejection.</summary>
public sealed class CashTransferTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        _ids = await CodTestData.SeedAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<CashTransferDto> Use(Func<ICashTransferService, Task<CashTransferDto>> action) => _context.UseAsync(action);

    private async Task<CashTransferDto> RequestAsync(CreateCashTransferRequest request, long userId = 70)
    {
        _context.CurrentUser.SignInAsSystemAdmin(userId);
        return await Use(s => s.CreateAsync(request, CancellationToken.None));
    }

    private async Task<CashTransferDto> ApproveAsync(long id, string? bankReference = null, long userId = 71)
    {
        _context.CurrentUser.SignInAsSystemAdmin(userId);
        return await Use(s => s.ApproveAsync(id, new ApproveCashTransferRequest { BankReference = bankReference }, CancellationToken.None));
    }

    private async Task<long> SecondDamascusCashboxAsync()
    {
        var syp = await _context.QueryAsync(db => db.Currencies.Where(x => x.Code == "SYP").Select(x => x.Id).SingleAsync());
        var box = await _context.UseAsync<ICashboxService, CashboxDto>(s => s.CreateAsync(new CreateCashboxRequest
        {
            Code = "DAM-SYP-02", ArabicName = "صندوق الكاونتر", BranchId = _ids.Damascus, CurrencyId = syp
        }, CancellationToken.None));
        return box.Id;
    }

    [Fact]
    public async Task SameBranchTransfer_IsCompletedWhenApproved()
    {
        var from = await CodTestData.CashboxIdAsync(_context);
        var to = await SecondDamascusCashboxAsync();

        var requested = await RequestAsync(new CreateCashTransferRequest { FromCashboxId = from, ToCashboxId = to, Amount = 120000m, Reason = "تغذية صندوق الكاونتر" });
        Assert.StartsWith("CTR-", requested.TransferNumber);
        Assert.Equal(CashTransferType.BetweenCashboxes, requested.TransferType);
        Assert.Equal(CashTransferStatus.PendingApproval, requested.Status);
        Assert.Equal(500000m, await CodTestData.CashboxBalanceAsync(_context));

        var approved = await ApproveAsync(requested.Id);

        Assert.Equal(CashTransferStatus.Completed, approved.Status);
        Assert.Equal(380000m, await CodTestData.CashboxBalanceAsync(_context));
        Assert.Equal(120000m, await CodTestData.CashboxBalanceAsync(_context, "DAM-SYP-02"));
        Assert.Empty(await CodTestData.UnbalancedPostingsAsync(_context));
    }

    [Fact]
    public async Task TheRequester_CannotApproveHisOwnTransfer()
    {
        var from = await CodTestData.CashboxIdAsync(_context);
        var to = await SecondDamascusCashboxAsync();
        var requested = await RequestAsync(new CreateCashTransferRequest { FromCashboxId = from, ToCashboxId = to, Amount = 1000m, Reason = "تجربة الاعتماد" });

        await Assert.ThrowsAsync<BusinessRuleException>(() => ApproveAsync(requested.Id, userId: 70));
    }

    [Fact]
    public async Task InterBranchTransfer_IsInTransitUntilTheDestinationReceivesIt()
    {
        var from = await CodTestData.CashboxIdAsync(_context);
        var aleppo = await CodTestData.CashboxIdAsync(_context, "ALP-SYP-01");

        var requested = await RequestAsync(new CreateCashTransferRequest { FromCashboxId = from, ToCashboxId = aleppo, Amount = 200000m, Reason = "دعم سيولة فرع حلب" });
        Assert.Equal(CashTransferType.BetweenBranches, requested.TransferType);

        var sent = await ApproveAsync(requested.Id);
        Assert.Equal(CashTransferStatus.InTransit, sent.Status);
        Assert.Equal(300000m, await CodTestData.CashboxBalanceAsync(_context));
        Assert.Equal(250000m, await CodTestData.CashboxBalanceAsync(_context, "ALP-SYP-01"));

        // A Damascus user cannot confirm the receipt for Aleppo.
        _context.CurrentUser.SignInAs(72, _ids.Damascus, Permissions.Cashboxes.View, Permissions.CashTransfers.Receive);
        await Assert.ThrowsAsync<ForbiddenException>(() => Use(s => s.ReceiveAsync(requested.Id, CancellationToken.None)));

        _context.CurrentUser.SignInAs(73, _ids.Aleppo, Permissions.Cashboxes.View, Permissions.CashTransfers.Receive);
        var received = await Use(s => s.ReceiveAsync(requested.Id, CancellationToken.None));

        Assert.Equal(CashTransferStatus.Completed, received.Status);
        Assert.Equal(450000m, await CodTestData.CashboxBalanceAsync(_context, "ALP-SYP-01"));
        Assert.Contains("CashTransferReceived", await _context.QueryAsync(db => db.OutboxMessages.Select(x => x.EventType).ToListAsync()));
    }

    [Fact]
    public async Task BankDeposit_NeedsTheDepositReference()
    {
        var from = await CodTestData.CashboxIdAsync(_context);
        var bank = await CodTestData.BankAccountIdAsync(_context);
        var requested = await RequestAsync(new CreateCashTransferRequest { FromCashboxId = from, ToBankAccountId = bank, Amount = 300000m, Reason = "إيداع نهاية الأسبوع" });
        Assert.Equal(CashTransferType.CashboxToBank, requested.TransferType);

        var missing = await Assert.ThrowsAsync<RequestValidationException>(() => ApproveAsync(requested.Id));
        Assert.True(missing.Errors.ContainsKey("bankReference"));

        var deposited = await ApproveAsync(requested.Id, bankReference: "DEP-55120");
        Assert.Equal(CashTransferStatus.Completed, deposited.Status);
        Assert.Equal("DEP-55120", deposited.BankReference);
        Assert.Equal(200000m, await CodTestData.CashboxBalanceAsync(_context));
    }

    [Fact]
    public async Task Transfers_MoreThanTheBalance_OrInAnotherCurrency_AreRefused()
    {
        var from = await CodTestData.CashboxIdAsync(_context);
        var usd = await CodTestData.CashboxIdAsync(_context, "DAM-USD-01");
        var to = await SecondDamascusCashboxAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            RequestAsync(new CreateCashTransferRequest { FromCashboxId = from, ToCashboxId = to, Amount = 600000m, Reason = "أكثر من الرصيد" }));

        var currency = await Assert.ThrowsAsync<RequestValidationException>(() =>
            RequestAsync(new CreateCashTransferRequest { FromCashboxId = from, ToCashboxId = usd, Amount = 1000m, Reason = "عملة مختلفة" }));
        Assert.True(currency.Errors.ContainsKey("toCashboxId"));
    }

    [Fact]
    public async Task RejectedTransfer_MovesNothing()
    {
        var from = await CodTestData.CashboxIdAsync(_context);
        var to = await SecondDamascusCashboxAsync();
        var requested = await RequestAsync(new CreateCashTransferRequest { FromCashboxId = from, ToCashboxId = to, Amount = 1000m, Reason = "طلب غير ضروري" });

        _context.CurrentUser.SignInAsSystemAdmin(71);
        var rejected = await Use(s => s.RejectAsync(requested.Id, new CashTransferReasonRequest { Reason = "لا حاجة للتحويل" }, CancellationToken.None));

        Assert.Equal(CashTransferStatus.Rejected, rejected.Status);
        Assert.Equal(500000m, await CodTestData.CashboxBalanceAsync(_context));
        await Assert.ThrowsAsync<BusinessRuleException>(() => ApproveAsync(requested.Id));
    }
}
