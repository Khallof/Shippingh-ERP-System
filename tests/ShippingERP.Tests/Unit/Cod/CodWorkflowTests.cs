using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Application.Interfaces.Cod;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Cod;

/// <summary>
/// COD life cycle: automatic COD record, assignment, collection (driver / counter), handover with shortage and
/// excess, idempotency, cancelled handover, ready for settlement with deductions, disputes.
/// </summary>
public sealed class CodWorkflowTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DemoIds _ids = null!;

    public async Task InitializeAsync()
    {
        _ids = await CodTestData.SeedAsync(_context);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    // ------------------------------------------------------------------ automatic COD record

    [Fact]
    public async Task CreatingACodShipment_CreatesAPendingCodRecord_AndANonCodShipmentDoesNot()
    {
        var cod = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, codAmount: 75000m));
        var normal = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids));

        var record = await CodTestData.GetCodAsync(_context, await CodTestData.CodIdAsync(_context, cod.Id));
        Assert.Equal(CODStatus.Pending, record.Status);
        Assert.Equal(75000m, record.ExpectedAmount);
        Assert.Equal(cod.SenderCustomerId, record.SenderCustomerId);
        Assert.Equal(CODTransactionType.Registered, Assert.Single(record.History).TransactionType);
        Assert.Equal(CODStatus.Pending, (await ShipmentTestData.GetAsync(_context, cod.Id)).CodStatus);

        Assert.False(await _context.QueryAsync(db => db.CODTransactions.AnyAsync(x => x.ShipmentId == normal.Id)));
        Assert.Null((await ShipmentTestData.GetAsync(_context, normal.Id)).CodStatus);
    }

    [Fact]
    public async Task DriverManifest_AssignsTheCodToTheDriver_AndAFailedAttemptPutsItBackToPending()
    {
        var shipment = await CodTestData.CodShipmentAtBranchAsync(_context, _ids);
        var manifestId = await CodTestData.DispatchAsync(_context, _ids.DamascusDriver, shipment);
        var codId = await CodTestData.CodIdAsync(_context, shipment.Id);

        var assigned = await CodTestData.GetCodAsync(_context, codId);
        Assert.Equal(CODStatus.AssignedToDriver, assigned.Status);
        Assert.Equal(_ids.DamascusDriver, assigned.DriverId);

        await _context.UseAsync<IDriverManifestService, DriverManifestDto>(s =>
            s.CloseAsync(manifestId, new CloseDriverManifestRequest { PendingReason = "المستلم غير موجود" }, CancellationToken.None));

        var back = await CodTestData.GetCodAsync(_context, codId);
        Assert.Equal(CODStatus.Pending, back.Status);
        Assert.Null(back.DriverId);
        Assert.Contains(back.History, x => x.TransactionType == CODTransactionType.Unassigned);
    }

    [Fact]
    public async Task CancellingTheShipment_CancelsItsCod()
    {
        var shipment = await ShipmentTestData.CreateAsync(_context, ShipmentTestData.NewRequest(_ids, codAmount: 50000m));

        await _context.UseAsync<IShipmentService, ShipmentDto>(s =>
            s.CancelAsync(shipment.Id, new CancelShipmentRequest { Reason = "طلب المرسل" }, CancellationToken.None));

        var record = await CodTestData.GetCodAsync(_context, await CodTestData.CodIdAsync(_context, shipment.Id));
        Assert.Equal(CODStatus.Cancelled, record.Status);
        Assert.NotNull(record.CancelledAtUtc);
    }

    // ------------------------------------------------------------------ collection

    [Fact]
    public async Task DriverCollection_PutsTheMoneyOnTheDriversBalance_AsSenderMoney()
    {
        var shipment = await CodTestData.DeliveredCodShipmentAsync(_context, _ids);

        var collected = await CodTestData.CollectAsync(_context, shipment.Id);

        Assert.Equal(CODStatus.CollectedByDriver, collected.Status);
        Assert.Equal(CodTestData.Cod, collected.CollectedAmount);
        Assert.Equal(_ids.DamascusDriver, collected.DriverId);
        Assert.Equal(CodTestData.Cod, await CodTestData.DriverBalanceAsync(_context, _ids.DamascusDriver));

        var movement = await _context.QueryAsync(db => db.DriverCashMovements.SingleAsync());
        Assert.Equal(DriverCashMovementType.CodCollected, movement.MovementType);
        Assert.Equal(CodTestData.Cod, movement.Debit);

        // The event carries a balanced entry that credits the sender liability, never revenue.
        Assert.Contains(await _context.QueryAsync(db => db.OutboxMessages.Select(x => x.EventType).ToListAsync()), x => x == "CodCollected");
        Assert.Empty(await CodTestData.UnbalancedPostingsAsync(_context));
        Assert.Contains("CodPayableToSenders", await _context.QueryAsync(db => db.OutboxMessages.Where(x => x.EventType == "CodCollected").Select(x => x.PayloadJson).SingleAsync()));
    }

    [Fact]
    public async Task Collection_BeforeDelivery_IsRefused()
    {
        var shipment = await CodTestData.CodShipmentAtBranchAsync(_context, _ids);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CodTestData.CollectAsync(_context, shipment.Id));
    }

    [Fact]
    public async Task Collection_Twice_IsAConflict()
    {
        var shipment = await CodTestData.DeliveredCodShipmentAsync(_context, _ids);
        await CodTestData.CollectAsync(_context, shipment.Id);

        await Assert.ThrowsAsync<ConflictException>(() => CodTestData.CollectAsync(_context, shipment.Id));
        Assert.Equal(CodTestData.Cod, await CodTestData.DriverBalanceAsync(_context, _ids.DamascusDriver));
    }

    [Fact]
    public async Task Collection_OfADifferentAmount_NeedsAReason_AndOpensADispute()
    {
        var shipment = await CodTestData.DeliveredCodShipmentAsync(_context, _ids);

        var error = await Assert.ThrowsAsync<RequestValidationException>(() => CodTestData.CollectAsync(_context, shipment.Id, 95000m));
        Assert.True(error.Errors.ContainsKey("differenceReason"));

        var disputed = await CodTestData.CollectAsync(_context, shipment.Id, 95000m, differenceReason: "المستلم دفع 95 ألفاً بموافقة المرسل");
        Assert.Equal(CODStatus.Disputed, disputed.Status);
        Assert.Equal(CODStatus.CollectedByDriver, disputed.StatusBeforeDispute);
        Assert.Equal(95000m, await CodTestData.DriverBalanceAsync(_context, _ids.DamascusDriver));
    }

    [Fact]
    public async Task CounterCollection_GoesStraightIntoTheCashbox()
    {
        // Delivered at the counter: no driver involved.
        var shipment = await CodTestData.CodShipmentAtBranchAsync(_context, _ids);
        await CodTestData.DeliverAsync(_context, shipment.Id);
        var cashboxId = await CodTestData.CashboxIdAsync(_context);
        var before = await CodTestData.CashboxBalanceAsync(_context);

        var collected = await CodTestData.CollectAsync(_context, shipment.Id, cashboxId: cashboxId);

        Assert.Equal(CODStatus.ReceivedAtBranchCashbox, collected.Status);
        Assert.Equal(cashboxId, collected.ReceivedCashboxId);
        Assert.StartsWith("RCV-", collected.ReceiptVoucherNumber);
        Assert.Equal(before + CodTestData.Cod, await CodTestData.CashboxBalanceAsync(_context));
        Assert.Equal(0m, await CodTestData.DriverBalanceAsync(_context, _ids.DamascusDriver));
    }

    [Fact]
    public async Task CounterCollection_NeedsTheHandoverPermission()
    {
        var shipment = await CodTestData.CodShipmentAtBranchAsync(_context, _ids);
        await CodTestData.DeliverAsync(_context, shipment.Id);
        var cashboxId = await CodTestData.CashboxIdAsync(_context);
        _context.CurrentUser.SignInAs(900, _ids.Damascus, Permissions.Cod.View, Permissions.Cod.Collect);

        await Assert.ThrowsAsync<ForbiddenException>(() => CodTestData.CollectAsync(_context, shipment.Id, cashboxId: cashboxId));
    }

    [Fact]
    public async Task DriverUser_CollectsOnlyForHimself_AndOnlySeesHisOwnShipments()
    {
        await _context.SeedRolesAndPermissionsAsync();
        var otherDriver = await CodTestData.CreateDamascusDriverAsync(_context, _ids);
        var mine = await CodTestData.DeliveredCodShipmentAsync(_context, _ids);
        var notMine = await CodTestData.DeliveredCodShipmentAsync(_context, _ids, driverId: otherDriver);
        var userId = await ShipmentTestData.LinkDriverUserAsync(_context, _ids.DamascusDriver, "driver.ali", _ids.Damascus);

        _context.CurrentUser.SignInWithRole(userId, _ids.Damascus, Roles.Driver, Permissions.Shipments.View, Permissions.Cod.View, Permissions.Cod.Collect);

        var collected = await CodTestData.CollectAsync(_context, mine.Id);
        Assert.Equal(_ids.DamascusDriver, collected.DriverId);
        await Assert.ThrowsAsync<NotFoundException>(() => CodTestData.CollectAsync(_context, notMine.Id));

        var list = await _context.UseAsync<ICodService, Application.Common.Models.PagedResult<CodTransactionListItemDto>>(s =>
            s.GetPagedAsync(new CodTransactionListQuery(), CancellationToken.None));
        Assert.All(list.Items, x => Assert.Equal(_ids.DamascusDriver, x.DriverId));
    }

    // ------------------------------------------------------------------ handover

    [Fact]
    public async Task Handover_MovesTheMoneyFromTheDriverToTheCashbox()
    {
        var first = await CodTestData.CollectedCodAsync(_context, _ids);
        var second = await CodTestData.CollectedCodAsync(_context, _ids, cod: 40000m);
        var before = await CodTestData.CashboxBalanceAsync(_context);

        var result = await CodTestData.HandoverAsync(_context, _ids.DamascusDriver, 140000m);

        var voucher = result.Voucher;
        Assert.Equal(CashReceiptType.DriverHandover, voucher.ReceiptType);
        Assert.Equal(140000m, voucher.ExpectedAmount);
        Assert.Equal(0m, voucher.ShortageAmount);
        Assert.Equal(2, voucher.CodLines.Count);
        Assert.Equal(0m, voucher.DriverBalanceAfter);
        Assert.Contains("ألفاً", voucher.AmountInWords);

        Assert.Equal(before + 140000m, await CodTestData.CashboxBalanceAsync(_context));
        Assert.Equal(0m, await CodTestData.DriverBalanceAsync(_context, _ids.DamascusDriver));
        Assert.Equal(CODStatus.ReceivedAtBranchCashbox, (await CodTestData.GetCodAsync(_context, first.Id)).Status);
        Assert.Equal(CODStatus.ReceivedAtBranchCashbox, (await CodTestData.GetCodAsync(_context, second.Id)).Status);
        Assert.Empty(await CodTestData.UnbalancedPostingsAsync(_context));
    }

    [Fact]
    public async Task Handover_WithAShortage_LeavesTheDifferenceOnTheDriversBalance()
    {
        var cod = await CodTestData.CollectedCodAsync(_context, _ids);

        var result = await CodTestData.HandoverAsync(_context, _ids.DamascusDriver, 97000m, codIds: new[] { cod.Id });

        Assert.Equal(3000m, result.Voucher.ShortageAmount);
        Assert.Equal(3000m, await CodTestData.DriverBalanceAsync(_context, _ids.DamascusDriver));
        // The sender is owed the full amount anyway: the COD is received.
        Assert.Equal(CODStatus.ReceivedAtBranchCashbox, (await CodTestData.GetCodAsync(_context, cod.Id)).Status);
    }

    [Fact]
    public async Task Handover_AboveTheDriversBalance_NeedsAnApprovedExcessAdjustment()
    {
        await CodTestData.CollectedCodAsync(_context, _ids);
        await Assert.ThrowsAsync<BusinessRuleException>(() => CodTestData.HandoverAsync(_context, _ids.DamascusDriver, 105000m));

        _context.CurrentUser.SignInAsSystemAdmin(1);
        var request = await _context.UseAsync<ICodAdjustmentService, CodAdjustmentDto>(s => s.CreateAsync(new CreateCodAdjustmentRequest
        {
            AdjustmentType = CODAdjustmentType.DriverExcessCash,
            DriverId = _ids.DamascusDriver,
            Amount = 5000m,
            Reason = "زيادة من تحصيل شحنة لم تُسجل"
        }, CancellationToken.None));
        _context.CurrentUser.SignInAsSystemAdmin(2);
        await _context.UseAsync<ICodAdjustmentService, CodAdjustmentDto>(s => s.ApproveAsync(request.Id, new DecideCodAdjustmentRequest(), CancellationToken.None));

        var result = await CodTestData.HandoverAsync(_context, _ids.DamascusDriver, 105000m);
        Assert.Equal(5000m, result.Voucher.ExcessAmount);
        Assert.Equal(0m, await CodTestData.DriverBalanceAsync(_context, _ids.DamascusDriver));
    }

    [Fact]
    public async Task Handover_WithTheSameIdempotencyKey_IsRecordedOnce()
    {
        await CodTestData.CollectedCodAsync(_context, _ids);
        var key = Guid.NewGuid().ToString();

        var first = await CodTestData.HandoverAsync(_context, _ids.DamascusDriver, 100000m, idempotencyKey: key);
        var retry = await CodTestData.HandoverAsync(_context, _ids.DamascusDriver, 100000m, idempotencyKey: key);

        Assert.False(first.IsReplay);
        Assert.True(retry.IsReplay);
        Assert.Equal(first.Voucher.Id, retry.Voucher.Id);
        Assert.Equal(1, await _context.QueryAsync(db => db.CashReceiptVouchers.CountAsync(x => x.ReceiptType == CashReceiptType.DriverHandover)));

        await Assert.ThrowsAsync<ConflictException>(() => CodTestData.HandoverAsync(_context, _ids.DamascusDriver, 50000m, idempotencyKey: key));
    }

    [Fact]
    public async Task CancellingTheHandoverReceipt_GivesTheMoneyBackToTheDriver()
    {
        var cod = await CodTestData.CollectedCodAsync(_context, _ids);
        var before = await CodTestData.CashboxBalanceAsync(_context);
        var handover = await CodTestData.HandoverAsync(_context, _ids.DamascusDriver, 100000m);

        var cancelled = await _context.UseAsync<ICashVoucherService, CashReceiptVoucherDto>(s =>
            s.CancelReceiptAsync(handover.Voucher.Id, new CancelCashVoucherRequest { Reason = "سُجّل على الصندوق الخطأ" }, CancellationToken.None));

        Assert.Equal(CashVoucherStatus.Cancelled, cancelled.Status);
        Assert.Single(cancelled.CodLines);
        Assert.Equal(before, await CodTestData.CashboxBalanceAsync(_context));
        Assert.Equal(CodTestData.Cod, await CodTestData.DriverBalanceAsync(_context, _ids.DamascusDriver));
        Assert.Equal(CODStatus.CollectedByDriver, (await CodTestData.GetCodAsync(_context, cod.Id)).Status);

        var reversal = await _context.QueryAsync(db => db.CashboxTransactions.SingleAsync(x => x.TransactionType == CashboxTransactionType.Reversal));
        Assert.Equal(100000m, reversal.AmountOut);
        Assert.NotNull(reversal.ReversedTransactionId);
    }

    // ------------------------------------------------------------------ ready for settlement

    [Fact]
    public async Task MarkReady_DeductsTheShippingChargeCommissionAndTax()
    {
        var ready = await CodTestData.ReadyCodAsync(_context, _ids);

        Assert.Equal(CODStatus.ReadyForSenderSettlement, ready.Status);
        Assert.Equal(15000m, ready.ShippingChargeDeduction);
        Assert.Equal(3000m, ready.CodCommissionDeduction);
        Assert.Equal(0m, ready.TaxDeduction);
        Assert.True(ready.ShippingChargeDeducted);
        Assert.Equal(CodTestData.Net, ready.NetPayableAmount);
        Assert.Equal(CodTestData.Net, ready.RemainingPayable);
    }

    [Fact]
    public async Task ChargeDeductedFromTheCod_CannotBeInvoicedAgain()
    {
        var ready = await CodTestData.ReadyCodAsync(_context, _ids);

        await Assert.ThrowsAsync<BusinessRuleException>(() => BillingTestData.InvoiceShipmentAsync(_context, ready.ShipmentId));
    }

    [Fact]
    public async Task InvoicedShipment_IsNotChargedTwice_OnlyTheCodIsPaidBack()
    {
        var collected = await CodTestData.CollectedCodAsync(_context, _ids);
        await BillingTestData.InvoiceShipmentAsync(_context, collected.ShipmentId, issue: true);
        await CodTestData.HandoverAsync(_context, _ids.DamascusDriver, CodTestData.Cod);

        await CodTestData.MarkReadyAsync(_context, collected.Id);
        var ready = await CodTestData.GetCodAsync(_context, collected.Id);

        Assert.False(ready.ShippingChargeDeducted);
        Assert.Equal(0m, ready.ShippingChargeDeduction + ready.CodCommissionDeduction + ready.TaxDeduction);
        Assert.Equal(CodTestData.Cod, ready.NetPayableAmount);
    }

    [Fact]
    public async Task MarkReady_RefusesCodStillWithTheDriver()
    {
        var collected = await CodTestData.CollectedCodAsync(_context, _ids);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CodTestData.MarkReadyAsync(_context, collected.Id));
    }

    // ------------------------------------------------------------------ disputes

    [Fact]
    public async Task Dispute_BlocksTheCod_AndIsResolvedByAnotherUser()
    {
        var collected = await CodTestData.CollectedCodAsync(_context, _ids);
        await CodTestData.HandoverAsync(_context, _ids.DamascusDriver, CodTestData.Cod);

        _context.CurrentUser.SignInAsSystemAdmin(10);
        var disputed = await _context.UseAsync<ICodService, CodTransactionDto>(s =>
            s.OpenDisputeAsync(collected.Id, new OpenCodDisputeRequest { Reason = "المرسل يعترض على المبلغ" }, CancellationToken.None));
        Assert.Equal(CODStatus.Disputed, disputed.Status);
        await Assert.ThrowsAsync<BusinessRuleException>(() => CodTestData.MarkReadyAsync(_context, collected.Id));

        await Assert.ThrowsAsync<BusinessRuleException>(() => _context.UseAsync<ICodService, CodTransactionDto>(s =>
            s.ResolveDisputeAsync(collected.Id, new ResolveCodDisputeRequest { Notes = "تمت مراجعة الإيصالات" }, CancellationToken.None)));

        _context.CurrentUser.SignInAsSystemAdmin(11);
        var resolved = await _context.UseAsync<ICodService, CodTransactionDto>(s =>
            s.ResolveDisputeAsync(collected.Id, new ResolveCodDisputeRequest { Notes = "تمت مراجعة الإيصالات" }, CancellationToken.None));
        Assert.Equal(CODStatus.ReceivedAtBranchCashbox, resolved.Status);
        Assert.Null(resolved.StatusBeforeDispute);
    }

    [Fact]
    public async Task EveryMoneyEvent_CarriesABalancedPostingRequest()
    {
        var ready = await CodTestData.ReadyCodAsync(_context, _ids);
        var settlement = await CodTestData.CreateSettlementAsync(_context, ready.SenderCustomerId);
        await CodTestData.ApproveSettlementAsync(_context, settlement.Id);
        await CodTestData.PayCashAsync(_context, settlement.Id);

        var types = await _context.QueryAsync(db => db.OutboxMessages.Select(x => x.EventType).ToListAsync());
        Assert.Contains("CashboxOpeningBalance", types);
        Assert.Contains("CodCollected", types);
        Assert.Contains("DriverCashHandedOver", types);
        Assert.Contains("SenderSettlementPaid", types);
        Assert.Empty(await CodTestData.UnbalancedPostingsAsync(_context));
    }
}
