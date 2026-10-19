using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Cash;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Cash;

/// <summary>Cashboxes, vouchers, daily closing and the daily cash movement report.</summary>
public sealed class CashboxTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        _ids = await CodTestData.SeedAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<T> Cashboxes<T>(Func<ICashboxService, Task<T>> action) => _context.UseAsync(action);

    private Task<T> Vouchers<T>(Func<ICashVoucherService, Task<T>> action) => _context.UseAsync(action);

    private async Task<long> SypId() => await _context.QueryAsync(db => db.Currencies.Where(x => x.Code == "SYP").Select(x => x.Id).SingleAsync());

    // ------------------------------------------------------------------ cashboxes

    [Fact]
    public async Task NewCashbox_WithAnOpeningBalance_HasItsFirstTransactionAndEvent()
    {
        var syp = await SypId();
        var cashbox = await Cashboxes(s => s.CreateAsync(new CreateCashboxRequest
        {
            Code = "dam-syp-02", ArabicName = "صندوق الكاونتر", BranchId = _ids.Damascus, CurrencyId = syp, OpeningBalance = 75000m
        }, CancellationToken.None));

        Assert.Equal("DAM-SYP-02", cashbox.Code);
        Assert.Equal(75000m, cashbox.CurrentBalance);

        var transactions = await Cashboxes(s => s.GetTransactionsAsync(cashbox.Id, new CashboxTransactionQuery(), CancellationToken.None));
        var opening = Assert.Single(transactions.Items);
        Assert.Equal(CashboxTransactionType.OpeningBalance, opening.TransactionType);
        Assert.Equal(75000m, opening.BalanceAfter);

        await Assert.ThrowsAsync<ConflictException>(() => Cashboxes(s => s.CreateAsync(new CreateCashboxRequest
        {
            Code = "DAM-SYP-02", ArabicName = "مكرر", BranchId = _ids.Damascus, CurrencyId = syp
        }, CancellationToken.None)));
    }

    [Fact]
    public async Task ACashboxWithMoney_CannotBeDeactivated()
    {
        var id = await CodTestData.CashboxIdAsync(_context);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Cashboxes(s => s.UpdateAsync(id,
            new UpdateCashboxRequest { ArabicName = "صندوق دمشق الرئيسي", IsActive = false }, CancellationToken.None)));
    }

    [Fact]
    public async Task BranchUsers_OnlySeeTheCashboxesOfTheirBranch()
    {
        var damascus = await CodTestData.CashboxIdAsync(_context);
        _context.CurrentUser.SignInAs(40, _ids.Aleppo, Permissions.Cashboxes.View);

        var list = await Cashboxes(s => s.GetPagedAsync(new CashboxListQuery(), CancellationToken.None));

        Assert.All(list.Items, x => Assert.Equal(_ids.Aleppo, x.BranchId));
        await Assert.ThrowsAsync<NotFoundException>(() => Cashboxes(s => s.GetByIdAsync(damascus, CancellationToken.None)));
    }

    // ------------------------------------------------------------------ vouchers

    [Fact]
    public async Task ReceiptAndPaymentVouchers_MoveTheBalance_AndPrintTheAmountInWords()
    {
        var id = await CodTestData.CashboxIdAsync(_context);

        var receipt = await Vouchers(s => s.CreateReceiptAsync(new CreateCashReceiptRequest
        {
            CashboxId = id, ReceiptType = CashReceiptType.OtherIncome, Amount = 25000m, ReceivedFrom = "بيع كراتين تالفة", Description = "إيراد متفرقات"
        }, null, CancellationToken.None));
        var payment = await CodTestData.PayExpenseAsync(_context, id, 15000m);

        Assert.StartsWith("RCV-", receipt.Voucher.VoucherNumber);
        Assert.StartsWith("PMV-", payment.Voucher.VoucherNumber);
        Assert.Equal("فقط خمسة عشر ألفاً ليرة سورية لا غير", payment.Voucher.AmountInWords);
        Assert.Equal(500000m + 25000m - 15000m, await CodTestData.CashboxBalanceAsync(_context));

        var statement = await Cashboxes(s => s.GetTransactionsAsync(id, new CashboxTransactionQuery(), CancellationToken.None));
        Assert.Equal(510000m, statement.Items[0].BalanceAfter);
        Assert.Equal(CashboxTransactionType.CashPayment, statement.Items[0].TransactionType);
        Assert.Empty(await CodTestData.UnbalancedPostingsAsync(_context));
    }

    [Fact]
    public async Task Payment_LargerThanTheBalance_IsRefused()
    {
        var id = await CodTestData.CashboxIdAsync(_context, "ALP-SYP-01");

        await Assert.ThrowsAsync<BusinessRuleException>(() => CodTestData.PayExpenseAsync(_context, id, 250001m));
        Assert.Equal(250000m, await CodTestData.CashboxBalanceAsync(_context, "ALP-SYP-01"));
    }

    [Fact]
    public async Task PaymentVoucher_IdempotencyKey_RecordsTheMoneyOnce()
    {
        var id = await CodTestData.CashboxIdAsync(_context);
        var key = Guid.NewGuid().ToString();

        var first = await CodTestData.PayExpenseAsync(_context, id, 10000m, key);
        var retry = await CodTestData.PayExpenseAsync(_context, id, 10000m, key);

        Assert.True(retry.IsReplay);
        Assert.Equal(first.Voucher.Id, retry.Voucher.Id);
        Assert.Equal(490000m, await CodTestData.CashboxBalanceAsync(_context));
    }

    [Fact]
    public async Task CancellingAPaymentVoucher_ReturnsTheMoney()
    {
        var id = await CodTestData.CashboxIdAsync(_context);
        var payment = await CodTestData.PayExpenseAsync(_context, id, 20000m);

        var cancelled = await Vouchers(s => s.CancelPaymentAsync(payment.Voucher.Id, new CancelCashVoucherRequest { Reason = "أُدخل مرتين" }, CancellationToken.None));

        Assert.Equal(CashVoucherStatus.Cancelled, cancelled.Status);
        Assert.Equal(500000m, await CodTestData.CashboxBalanceAsync(_context));
        await Assert.ThrowsAsync<ConflictException>(() => Vouchers(s => s.CancelPaymentAsync(payment.Voucher.Id, new CancelCashVoucherRequest { Reason = "مرة ثانية" }, CancellationToken.None)));
    }

    [Fact]
    public async Task CustomerCashPayment_IsDepositedOnce_WithItsExactAmount()
    {
        var customerId = _ids.Customer("CUS-00003");
        var payment = await BillingTestData.PayAsync(_context, customerId, 30000m);
        var id = await CodTestData.CashboxIdAsync(_context);

        CreateCashReceiptRequest Request(decimal amount) => new()
        {
            CashboxId = id, ReceiptType = CashReceiptType.CustomerPayment, Amount = amount, ReceivedFrom = "العميل",
            Description = "إيداع دفعة نقدية", CustomerPaymentId = payment.Payment.Id
        };

        var wrongAmount = await Assert.ThrowsAsync<RequestValidationException>(() => Vouchers(s => s.CreateReceiptAsync(Request(25000m), null, CancellationToken.None)));
        Assert.True(wrongAmount.Errors.ContainsKey("amount"));

        var deposit = await Vouchers(s => s.CreateReceiptAsync(Request(30000m), null, CancellationToken.None));
        Assert.Equal(customerId, deposit.Voucher.CustomerId);
        Assert.Equal(payment.Payment.PaymentNumber, deposit.Voucher.CustomerPaymentNumber);

        await Assert.ThrowsAsync<ConflictException>(() => Vouchers(s => s.CreateReceiptAsync(Request(30000m), null, CancellationToken.None)));
    }

    [Fact]
    public async Task VoucherPdfs_AreGenerated()
    {
        var id = await CodTestData.CashboxIdAsync(_context);
        var payment = await CodTestData.PayExpenseAsync(_context, id, 12500m);
        await CodTestData.CollectedCodAsync(_context, _ids);
        var handover = await CodTestData.HandoverAsync(_context, _ids.DamascusDriver, CodTestData.Cod);

        foreach (var file in new[]
                 {
                     await Vouchers(s => s.GetPaymentPdfAsync(payment.Voucher.Id, CancellationToken.None)),
                     await Vouchers(s => s.GetReceiptPdfAsync(handover.Voucher.Id, CancellationToken.None))
                 })
        {
            using var memory = new MemoryStream();
            await file.Content.CopyToAsync(memory);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(memory.ToArray(), 0, 4));
        }
    }

    // ------------------------------------------------------------------ daily closing

    [Fact]
    public async Task ClosingWithoutDifference_ClosesTheDay_AndBlocksNewMovements()
    {
        var id = await CodTestData.CashboxIdAsync(_context);

        var closing = await Cashboxes(s => s.CloseDayAsync(id, new CloseCashboxDayRequest { CountedBalance = 500000m }, CancellationToken.None));

        Assert.Equal(CashboxClosingStatus.Closed, closing.Status);
        Assert.Equal(0m, closing.OpeningBalance);
        Assert.Equal(500000m, closing.TotalIn);
        Assert.Equal(500000m, closing.ExpectedBalance);
        Assert.Equal(0m, closing.Discrepancy);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CodTestData.PayExpenseAsync(_context, id, 1000m));
        await Assert.ThrowsAsync<ConflictException>(() => Cashboxes(s => s.CloseDayAsync(id, new CloseCashboxDayRequest { CountedBalance = 500000m }, CancellationToken.None)));
    }

    [Fact]
    public async Task ClosingDifference_NeedsAReason_AndASecondUserToPostTheShortage()
    {
        var id = await CodTestData.CashboxIdAsync(_context);

        var noReason = await Assert.ThrowsAsync<RequestValidationException>(() => Cashboxes(s =>
            s.CloseDayAsync(id, new CloseCashboxDayRequest { CountedBalance = 499000m }, CancellationToken.None)));
        Assert.True(noReason.Errors.ContainsKey("discrepancyReason"));

        _context.CurrentUser.SignInAsSystemAdmin(50);
        var closing = await Cashboxes(s => s.CloseDayAsync(id,
            new CloseCashboxDayRequest { CountedBalance = 499000m, DiscrepancyReason = "ورقة نقدية مزورة" }, CancellationToken.None));
        Assert.Equal(CashboxClosingStatus.PendingApproval, closing.Status);
        Assert.Equal(-1000m, closing.Discrepancy);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Cashboxes(s => s.ApproveClosingAsync(closing.Id, new DecideCashboxClosingRequest(), CancellationToken.None)));

        _context.CurrentUser.SignInAsSystemAdmin(51);
        var approved = await Cashboxes(s => s.ApproveClosingAsync(closing.Id, new DecideCashboxClosingRequest { Notes = "تم التحقق" }, CancellationToken.None));

        Assert.Equal(CashboxClosingStatus.Approved, approved.Status);
        Assert.NotNull(approved.AdjustmentTransactionId);
        Assert.Equal(499000m, await CodTestData.CashboxBalanceAsync(_context));

        var discrepancies = await Cashboxes(s => s.GetClosingsAsync(new CashboxClosingQuery { HasDiscrepancy = true }, CancellationToken.None));
        Assert.Equal(closing.Id, Assert.Single(discrepancies.Items).Id);
        Assert.Contains("CashboxDiscrepancyPosted", await _context.QueryAsync(db => db.OutboxMessages.Select(x => x.EventType).ToListAsync()));
    }

    [Fact]
    public async Task RejectedClosing_ReopensTheDay()
    {
        var id = await CodTestData.CashboxIdAsync(_context);
        _context.CurrentUser.SignInAsSystemAdmin(60);
        var closing = await Cashboxes(s => s.CloseDayAsync(id,
            new CloseCashboxDayRequest { CountedBalance = 510000m, DiscrepancyReason = "زيادة غير معروفة" }, CancellationToken.None));

        _context.CurrentUser.SignInAsSystemAdmin(61);
        var rejected = await Cashboxes(s => s.RejectClosingAsync(closing.Id, new DecideCashboxClosingRequest { Notes = "أعد العد بحضور المدير" }, CancellationToken.None));

        Assert.Equal(CashboxClosingStatus.Rejected, rejected.Status);
        var payment = await CodTestData.PayExpenseAsync(_context, id, 1000m);
        Assert.Equal(499000m, await CodTestData.CashboxBalanceAsync(_context));
        Assert.NotNull(payment.Voucher);
    }

    // ------------------------------------------------------------------ report

    [Fact]
    public async Task DailyCashMovement_ShowsOpeningInOutAndClosing()
    {
        var id = await CodTestData.CashboxIdAsync(_context);
        await CodTestData.CollectedCodAsync(_context, _ids);
        await CodTestData.HandoverAsync(_context, _ids.DamascusDriver, CodTestData.Cod);
        await CodTestData.PayExpenseAsync(_context, id, 30000m);

        var report = await _context.UseAsync<ICashReportService, DailyCashMovementReportDto>(s =>
            s.GetDailyCashMovementAsync(new DailyCashMovementQuery { CashboxId = id }, CancellationToken.None));

        var today = Assert.Single(report.Rows);
        Assert.Equal(0m, today.OpeningBalance);
        Assert.Equal(CodTestData.Cod, today.DriverHandovers);
        Assert.Equal(500000m, today.OtherIn);
        Assert.Equal(30000m, today.Payments);
        Assert.Equal(570000m, today.ClosingBalance);
        Assert.Equal(570000m, await CodTestData.CashboxBalanceAsync(_context));
        Assert.Equal("SYP", Assert.Single(report.Totals).CurrencyCode);
    }

    [Fact]
    public async Task DailyCashMovement_NextDay_StartsFromTheClosingBalance()
    {
        var id = await CodTestData.CashboxIdAsync(_context);
        await CodTestData.PayExpenseAsync(_context, id, 100000m);
        _context.Clock.Advance(TimeSpan.FromDays(1));
        await CodTestData.PayExpenseAsync(_context, id, 50000m);

        var report = await _context.UseAsync<ICashReportService, DailyCashMovementReportDto>(s =>
            s.GetDailyCashMovementAsync(new DailyCashMovementQuery { CashboxId = id }, CancellationToken.None));

        Assert.Equal(2, report.Rows.Count);
        Assert.Equal(400000m, report.Rows[0].ClosingBalance);
        Assert.Equal(400000m, report.Rows[1].OpeningBalance);
        Assert.Equal(350000m, report.Rows[1].ClosingBalance);
    }
}
