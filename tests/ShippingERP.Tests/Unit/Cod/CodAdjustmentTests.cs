using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Application.Interfaces.Cod;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Cod;

/// <summary>Adjustments with reason + approval: driver shortage / excess, collection correction / reversal, sender fee / compensation.</summary>
public sealed class CodAdjustmentTests : IAsyncLifetime
{
    private const long Requester = 30;
    private const long Approver = 31;

    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        _ids = await CodTestData.SeedAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private async Task<CodAdjustmentDto> RequestAsync(CreateCodAdjustmentRequest request)
    {
        _context.CurrentUser.SignInAsSystemAdmin(Requester);
        return await _context.UseAsync<ICodAdjustmentService, CodAdjustmentDto>(s => s.CreateAsync(request, CancellationToken.None));
    }

    private async Task<CodAdjustmentDto> ApproveAsync(long id)
    {
        _context.CurrentUser.SignInAsSystemAdmin(Approver);
        return await _context.UseAsync<ICodAdjustmentService, CodAdjustmentDto>(s => s.ApproveAsync(id, new DecideCodAdjustmentRequest { Notes = "موافق" }, CancellationToken.None));
    }

    [Fact]
    public async Task ShortageWriteOff_ChangesNothingUntilAnotherUserApprovesIt()
    {
        var cod = await CodTestData.CollectedCodAsync(_context, _ids);
        await CodTestData.HandoverAsync(_context, _ids.DamascusDriver, 97000m, codIds: new[] { cod.Id });
        Assert.Equal(3000m, await CodTestData.DriverBalanceAsync(_context, _ids.DamascusDriver));

        var request = await RequestAsync(new CreateCodAdjustmentRequest
        {
            AdjustmentType = CODAdjustmentType.DriverShortageWriteOff,
            DriverId = _ids.DamascusDriver,
            Amount = 3000m,
            Reason = "عجز ثابت بعد المراجعة يُحمّل على السائق"
        });

        Assert.StartsWith("CAD-", request.AdjustmentNumber);
        Assert.Equal(CODAdjustmentStatus.PendingApproval, request.Status);
        Assert.Equal(3000m, await CodTestData.DriverBalanceAsync(_context, _ids.DamascusDriver));

        // The requester cannot approve his own request.
        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.UseAsync<ICodAdjustmentService, CodAdjustmentDto>(s =>
            s.ApproveAsync(request.Id, new DecideCodAdjustmentRequest(), CancellationToken.None)));

        var approved = await ApproveAsync(request.Id);
        Assert.Equal(CODAdjustmentStatus.Approved, approved.Status);
        Assert.Equal(Approver, approved.DecidedByUserId);
        Assert.Equal(0m, await CodTestData.DriverBalanceAsync(_context, _ids.DamascusDriver));
        Assert.Contains("DriverCashAdjusted", await _context.QueryAsync(db => db.OutboxMessages.Select(x => x.EventType).ToListAsync()));
        Assert.Empty(await CodTestData.UnbalancedPostingsAsync(_context));
    }

    [Fact]
    public async Task ShortageWriteOff_AboveTheBalance_IsRefused()
    {
        await CodTestData.CollectedCodAsync(_context, _ids, cod: 10000m);

        await Assert.ThrowsAsync<BusinessRuleException>(() => RequestAsync(new CreateCodAdjustmentRequest
        {
            AdjustmentType = CODAdjustmentType.DriverShortageWriteOff,
            DriverId = _ids.DamascusDriver,
            Amount = 20000m,
            Reason = "مبلغ أكبر من العهدة"
        }));
    }

    [Fact]
    public async Task CollectionCorrection_FixesTheCodAndTheDriverBalance()
    {
        var cod = await CodTestData.CollectedCodAsync(_context, _ids);

        var request = await RequestAsync(new CreateCodAdjustmentRequest
        {
            AdjustmentType = CODAdjustmentType.CollectionCorrection,
            CodTransactionId = cod.Id,
            NewCollectedAmount = 90000m,
            Reason = "أُدخل المبلغ خطأ، الإيصال 90 ألفاً"
        });
        Assert.Equal(10000m, request.Amount);
        Assert.Equal(CodTestData.Cod, request.PreviousCollectedAmount);

        await ApproveAsync(request.Id);

        var corrected = await CodTestData.GetCodAsync(_context, cod.Id);
        Assert.Equal(90000m, corrected.CollectedAmount);
        Assert.Equal(CODStatus.CollectedByDriver, corrected.Status);
        Assert.Contains(corrected.History, x => x.TransactionType == CODTransactionType.CollectionCorrected);
        Assert.Equal(90000m, await CodTestData.DriverBalanceAsync(_context, _ids.DamascusDriver));
    }

    [Fact]
    public async Task CollectionReversal_PutsTheCodBackToPending()
    {
        var cod = await CodTestData.CollectedCodAsync(_context, _ids);

        var request = await RequestAsync(new CreateCodAdjustmentRequest
        {
            AdjustmentType = CODAdjustmentType.CollectionReversal,
            CodTransactionId = cod.Id,
            Reason = "سُجّل التحصيل على الشحنة الخطأ"
        });
        await ApproveAsync(request.Id);

        var reversed = await CodTestData.GetCodAsync(_context, cod.Id);
        Assert.Equal(CODStatus.Pending, reversed.Status);
        Assert.Equal(0m, reversed.CollectedAmount);
        Assert.Equal(0m, await CodTestData.DriverBalanceAsync(_context, _ids.DamascusDriver));

        // It can be collected again correctly.
        _context.CurrentUser.SignOut();
        var again = await CodTestData.CollectAsync(_context, cod.ShipmentId);
        Assert.Equal(CODStatus.CollectedByDriver, again.Status);
    }

    [Fact]
    public async Task CollectionCorrection_AfterTheHandover_IsRefused()
    {
        var cod = await CodTestData.CollectedCodAsync(_context, _ids);
        await CodTestData.HandoverAsync(_context, _ids.DamascusDriver, CodTestData.Cod);

        await Assert.ThrowsAsync<BusinessRuleException>(() => RequestAsync(new CreateCodAdjustmentRequest
        {
            AdjustmentType = CODAdjustmentType.CollectionCorrection,
            CodTransactionId = cod.Id,
            NewCollectedAmount = 90000m,
            Reason = "تصحيح متأخر"
        }));
    }

    [Fact]
    public async Task SenderFeeAndCompensation_ChangeTheNetOfAReadyCod()
    {
        var ready = await CodTestData.ReadyCodAsync(_context, _ids);

        var fee = await RequestAsync(new CreateCodAdjustmentRequest
        {
            AdjustmentType = CODAdjustmentType.SenderFee,
            CodTransactionId = ready.Id,
            Amount = 2500m,
            Reason = "رسوم تخزين أسبوع إضافي"
        });
        await ApproveAsync(fee.Id);

        var compensation = await RequestAsync(new CreateCodAdjustmentRequest
        {
            AdjustmentType = CODAdjustmentType.SenderCompensation,
            CodTransactionId = ready.Id,
            Amount = 1000m,
            Reason = "تعويض عن تأخير التسليم"
        });
        await ApproveAsync(compensation.Id);

        var cod = await CodTestData.GetCodAsync(_context, ready.Id);
        Assert.Equal(2500m, cod.OtherFeesAmount);
        Assert.Equal(1000m, cod.CompensationAmount);
        Assert.Equal(CodTestData.Net - 2500m + 1000m, cod.NetPayableAmount);

        _context.CurrentUser.SignOut();
        var settlement = await CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId);
        Assert.Equal(2500m, settlement.TotalOtherFees);
        Assert.Equal(1000m, settlement.TotalCompensation);
        Assert.Equal(CodTestData.Net - 1500m, settlement.NetPayable);
        Assert.Equal(CodTestData.Cod, settlement.TotalGrossReleased);
    }

    [Fact]
    public async Task SenderFee_OnAReservedCod_IsRefused()
    {
        var ready = await CodTestData.ReadyCodAsync(_context, _ids);
        await CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId);

        await Assert.ThrowsAsync<BusinessRuleException>(() => RequestAsync(new CreateCodAdjustmentRequest
        {
            AdjustmentType = CODAdjustmentType.SenderFee,
            CodTransactionId = ready.Id,
            Amount = 1000m,
            Reason = "رسوم إضافية متأخرة"
        }));
    }

    [Fact]
    public async Task Reject_NeedsNotes_AndChangesNothing()
    {
        await CodTestData.CollectedCodAsync(_context, _ids);
        var request = await RequestAsync(new CreateCodAdjustmentRequest
        {
            AdjustmentType = CODAdjustmentType.DriverExcessCash,
            DriverId = _ids.DamascusDriver,
            Amount = 5000m,
            Reason = "زيادة غير مبررة"
        });

        _context.CurrentUser.SignInAsSystemAdmin(Approver);
        await Assert.ThrowsAsync<RequestValidationException>(() => _context.UseAsync<ICodAdjustmentService, CodAdjustmentDto>(s =>
            s.RejectAsync(request.Id, new DecideCodAdjustmentRequest(), CancellationToken.None)));

        var rejected = await _context.UseAsync<ICodAdjustmentService, CodAdjustmentDto>(s =>
            s.RejectAsync(request.Id, new DecideCodAdjustmentRequest { Notes = "لا يوجد ما يثبت الزيادة" }, CancellationToken.None));

        Assert.Equal(CODAdjustmentStatus.Rejected, rejected.Status);
        Assert.Equal(CodTestData.Cod, await CodTestData.DriverBalanceAsync(_context, _ids.DamascusDriver));
        await Assert.ThrowsAsync<BusinessRuleException>(() => ApproveAsync(request.Id));
    }
}
