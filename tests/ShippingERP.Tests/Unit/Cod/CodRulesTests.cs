using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Cod;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Cash;

namespace ShippingERP.Tests.Unit.Cod;

/// <summary>Pure rules of step 6 (no database): COD state machine, settlement arithmetic, amount in words, postings.</summary>
public sealed class CodRulesTests
{
    // ------------------------------------------------------------------ state machine

    [Theory]
    [InlineData(CODStatus.Pending, CODStatus.AssignedToDriver)]
    [InlineData(CODStatus.AssignedToDriver, CODStatus.CollectedByDriver)]
    [InlineData(CODStatus.Pending, CODStatus.ReceivedAtBranchCashbox)]
    [InlineData(CODStatus.CollectedByDriver, CODStatus.ReceivedAtBranchCashbox)]
    [InlineData(CODStatus.ReceivedAtBranchCashbox, CODStatus.ReadyForSenderSettlement)]
    [InlineData(CODStatus.ReadyForSenderSettlement, CODStatus.PartiallySettled)]
    [InlineData(CODStatus.PartiallySettled, CODStatus.SettledToSender)]
    [InlineData(CODStatus.SettledToSender, CODStatus.ReadyForSenderSettlement)]
    [InlineData(CODStatus.CollectedByDriver, CODStatus.Disputed)]
    [InlineData(CODStatus.Disputed, CODStatus.ReceivedAtBranchCashbox)]
    [InlineData(CODStatus.Cancelled, CODStatus.Pending)]
    public void AllowedTransitions(CODStatus from, CODStatus to)
    {
        Assert.True(CodStatusRules.CanTransition(from, to));
    }

    [Theory]
    [InlineData(CODStatus.Pending, CODStatus.ReadyForSenderSettlement)]
    [InlineData(CODStatus.CollectedByDriver, CODStatus.SettledToSender)]
    [InlineData(CODStatus.ReadyForSenderSettlement, CODStatus.Pending)]
    [InlineData(CODStatus.SettledToSender, CODStatus.Cancelled)]
    [InlineData(CODStatus.Cancelled, CODStatus.CollectedByDriver)]
    [InlineData(CODStatus.SettledToSender, CODStatus.Disputed)]
    public void ForbiddenTransitions(CODStatus from, CODStatus to)
    {
        Assert.False(CodStatusRules.CanTransition(from, to));
    }

    [Fact]
    public void NotCollectedStatus_FollowsTheShipment()
    {
        Assert.Equal(CODStatus.AssignedToDriver, CodStatusRules.NotCollectedStatusFor(ShipmentStatus.OutForDelivery, hasDriver: true, CODStatus.Pending));
        Assert.Equal(CODStatus.Pending, CodStatusRules.NotCollectedStatusFor(ShipmentStatus.DeliveryFailed, hasDriver: true, CODStatus.AssignedToDriver));
        Assert.Equal(CODStatus.AssignedToDriver, CodStatusRules.NotCollectedStatusFor(ShipmentStatus.Delivered, hasDriver: true, CODStatus.AssignedToDriver));
        Assert.Equal(CODStatus.Pending, CodStatusRules.NotCollectedStatusFor(ShipmentStatus.InWarehouse, hasDriver: false, CODStatus.AssignedToDriver));
    }

    // ------------------------------------------------------------------ settlement arithmetic

    [Fact]
    public void NetPayable_IsCollectedMinusDeductionsPlusCompensation()
    {
        var deductions = new CodDeductions(ShippingCharge: 15000m, CodCommission: 3000m, Tax: 900m, OtherFees: 2000m, Compensation: 500m);

        Assert.Equal(100000m - 15000m - 3000m - 900m - 2000m + 500m, CodSettlementMath.NetPayable(100000m, deductions));
    }

    [Fact]
    public void PartialLines_RecognizeTheDeductionsOnce_AndReleaseExactlyTheCollectedMoney()
    {
        var deductions = new CodDeductions(15000m, 3000m, 0m, 0m, 0m);
        var net = CodSettlementMath.NetPayable(100000m, deductions); // 82,000

        var first = CodSettlementMath.PlanLine(net, settledAmount: 0m, deductionsRecognized: false, deductions, requestedAmount: 50000m, out var error1)!;
        var second = CodSettlementMath.PlanLine(net, settledAmount: 50000m, deductionsRecognized: true, deductions, requestedAmount: null, out var error2)!;

        Assert.Null(error1);
        Assert.Null(error2);
        Assert.True(first.RecognizesDeductions);
        Assert.Equal(68000m, first.GrossReleased);
        Assert.False(second.RecognizesDeductions);
        Assert.Equal(32000m, second.Amount);
        Assert.Equal(32000m, second.GrossReleased);
        Assert.Equal(100000m, first.GrossReleased + second.GrossReleased);
        Assert.Equal(net, first.Amount + second.Amount);
    }

    [Fact]
    public void PlanLine_RejectsMoreThanTheRemaining_AndNothingLeft()
    {
        var deductions = new CodDeductions(15000m, 3000m, 0m, 0m, 0m);

        Assert.Null(CodSettlementMath.PlanLine(82000m, 0m, false, deductions, 90000m, out var tooMuch));
        Assert.NotNull(tooMuch);
        Assert.Null(CodSettlementMath.PlanLine(82000m, 82000m, true, deductions, null, out var nothing));
        Assert.NotNull(nothing);
    }

    [Fact]
    public void NegativeNet_IsTakenInFull_AndCannotBeSplit()
    {
        var deductions = new CodDeductions(15000m, 2050m, 0m, 0m, 0m);
        var net = CodSettlementMath.NetPayable(5000m, deductions); // -12,050

        var full = CodSettlementMath.PlanLine(net, 0m, false, deductions, null, out _)!;
        Assert.Equal(-12050m, full.Amount);
        Assert.Equal(5000m, full.GrossReleased);

        Assert.Null(CodSettlementMath.PlanLine(net, 0m, false, deductions, -1000m, out var error));
        Assert.NotNull(error);
    }

    // ------------------------------------------------------------------ amount in words

    [Theory]
    [InlineData(0, "صفر")]
    [InlineData(1000, "ألف")]
    [InlineData(2000, "ألفان")]
    [InlineData(3000, "ثلاثة آلاف")]
    [InlineData(15000, "خمسة عشر ألفاً")]
    [InlineData(82000, "اثنان وثمانون ألفاً")]
    [InlineData(125500, "مائة وخمسة وعشرون ألفاً وخمسمائة")]
    [InlineData(1250000, "مليون ومائتان وخمسون ألفاً")]
    [InlineData(2000000, "مليونان")]
    public void AmountInWords(long number, string expected)
    {
        Assert.Equal(expected, ArabicAmountInWords.ToWords(number));
    }

    [Fact]
    public void AmountInWords_FullSentence_WithCurrencyAndFraction()
    {
        Assert.Equal("فقط خمسة عشر ألفاً ليرة سورية لا غير", ArabicAmountInWords.Format(15000m, "ليرة سورية", 0));
        Assert.Equal("فقط عشرون و 50/100 دولار أمريكي لا غير", ArabicAmountInWords.Format(20.5m, "دولار أمريكي", 2));
    }

    // ------------------------------------------------------------------ accounting posting requests

    [Fact]
    public void PostingRequest_MustBeBalanced()
    {
        Assert.Throws<InvalidOperationException>(() => AccountingPostingRequest.Create("Test", 1, "T-1", 1, DateTime.UtcNow, "x", new[]
        {
            AccountingPostingRequest.Debit(AccountRoles.CashOnHand, 100m),
            AccountingPostingRequest.Credit(AccountRoles.OtherIncome, 90m)
        }));
    }

    [Fact]
    public void PostingRequest_MovesNegativeAmountsToTheOtherSide_AndReverseSwapsThem()
    {
        var posting = CashPostings.ClosingDiscrepancy(1, "DAM-20261012", 1, DateTime.UtcNow, discrepancy: -500m, branchId: 1, cashboxId: 7);

        var cash = Assert.Single(posting.Lines, x => x.AccountRole == AccountRoles.CashOnHand);
        Assert.Equal(500m, cash.Credit);
        Assert.Equal(500m, Assert.Single(posting.Lines, x => x.AccountRole == AccountRoles.CashOverShort).Debit);

        var reversed = posting.Reverse("عكس", DateTime.UtcNow);
        Assert.True(reversed.IsReversal);
        Assert.Equal(500m, Assert.Single(reversed.Lines, x => x.AccountRole == AccountRoles.CashOnHand).Debit);
    }

    [Fact]
    public void SettlementPosting_ReleasesTheSenderMoney_AndRecognizesTheDeductionsAsRevenue()
    {
        // Collected 100,000; shipping 15,000; commission 3,000; tax 900; fee 1,000; compensation 400 -> net 80,500.
        var posting = CashPostings.SettlementPaid(1, "SST-1", 1, DateTime.UtcNow, branchId: 1, senderCustomerId: 3,
            grossReleased: 100000m, shipping: 15000m, commission: 3000m, tax: 900m, otherFees: 1000m, compensation: 400m,
            netPaid: 80500m, cashboxId: 7, bankAccountId: null);

        Assert.Equal(posting.TotalDebit, posting.TotalCredit);
        Assert.Equal(100000m, posting.Lines.Single(x => x.AccountRole == AccountRoles.CodPayableToSenders).Debit);
        Assert.Equal(15000m, posting.Lines.Single(x => x.AccountRole == AccountRoles.ShippingRevenue).Credit);
        Assert.Equal(3000m, posting.Lines.Single(x => x.AccountRole == AccountRoles.CodCommissionRevenue).Credit);
        Assert.Equal(80500m, posting.Lines.Single(x => x.AccountRole == AccountRoles.CashOnHand).Credit);

        // The COD amount itself never reaches a revenue account.
        Assert.DoesNotContain(posting.Lines, x => x.AccountRole == AccountRoles.ShippingRevenue && x.Credit == 100000m);
    }

    [Fact]
    public void CollectionPosting_IsALiabilityToTheSender_NotRevenue()
    {
        var posting = CashPostings.CodCollected(1, "SHP-1", 1, DateTime.UtcNow, 100000m, senderCustomerId: 3, branchId: 1, driverId: 4, cashboxId: null);

        Assert.Equal(100000m, posting.Lines.Single(x => x.AccountRole == AccountRoles.DriverCustody).Debit);
        Assert.Equal(100000m, posting.Lines.Single(x => x.AccountRole == AccountRoles.CodPayableToSenders).Credit);
        Assert.DoesNotContain(posting.Lines, x => x.AccountRole.EndsWith("Revenue", StringComparison.Ordinal));
    }
}
