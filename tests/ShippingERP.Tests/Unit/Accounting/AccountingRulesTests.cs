using ShippingERP.Application.IntegrationEvents;
using ShippingERP.Domain.Accounting;
using ShippingERP.Domain.Entities.Accounting;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Tests.Unit.Accounting;

/// <summary>
/// The pure accounting rules of the Domain (step 7): journal balance validation, the entry workflow, account sign
/// conventions, base-currency rounding, monthly periods, posting rule matching and FIFO aging.
/// </summary>
public sealed class AccountingRulesTests
{
    private static IReadOnlyList<JournalLineAmount> Lines(params (decimal Debit, decimal Credit)[] lines) =>
        lines.Select(x => new JournalLineAmount(x.Debit, x.Credit)).ToList();

    // ------------------------------------------------------------------ debits = credits

    [Fact]
    public void BalancedEntry_IsValid()
    {
        var lines = Lines((15_750m, 0m), (0m, 15_000m), (0m, 750m));

        Assert.Empty(JournalRules.Validate(lines));
        Assert.True(JournalRules.IsBalanced(lines));
    }

    [Fact]
    public void Entry_WhereDebitsDifferFromCredits_IsRejected()
    {
        var errors = JournalRules.Validate(Lines((10_000m, 0m), (0m, 9_999m)));

        Assert.Contains(errors, x => x.Contains("غير متوازن"));
        Assert.False(JournalRules.IsBalanced(Lines((10_000m, 0m), (0m, 9_999m))));
    }

    [Fact]
    public void Entry_NeedsTwoLines()
    {
        Assert.NotEmpty(JournalRules.Validate(Lines((0m, 0m))));
        Assert.NotEmpty(JournalRules.Validate(Lines()));
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(5, 5)]
    [InlineData(0, 0)]
    public void Line_MustHaveExactlyOnePositiveSide(int debit, int credit)
    {
        var errors = JournalRules.Validate(Lines((debit, credit), (0m, 100m), (100m, 0m)));

        Assert.Contains(errors, x => x.StartsWith("السطر 1"));
    }

    [Fact]
    public void Entry_WithTooManyLines_IsRejected()
    {
        var lines = Enumerable.Range(0, JournalRules.MaxLines + 1).Select(i => i % 2 == 0 ? (1m, 0m) : (0m, 1m)).ToArray();

        Assert.Contains(JournalRules.Validate(Lines(lines)), x => x.Contains(JournalRules.MaxLines.ToString()));
    }

    // ------------------------------------------------------------------ workflow

    [Theory]
    [InlineData(JournalEntryStatus.Draft, true)]
    [InlineData(JournalEntryStatus.Rejected, true)]
    [InlineData(JournalEntryStatus.Submitted, false)]
    [InlineData(JournalEntryStatus.Approved, false)]
    [InlineData(JournalEntryStatus.Posted, false)]
    [InlineData(JournalEntryStatus.Reversed, false)]
    public void OnlyDraftAndRejectedEntries_AreEditable(JournalEntryStatus status, bool editable)
    {
        Assert.Equal(editable, JournalRules.CanEdit(status));
        Assert.Equal(editable, JournalRules.CanDelete(status));
        Assert.Equal(editable, JournalRules.CanSubmit(status));
    }

    [Fact]
    public void Workflow_SubmitApprovePostReverse()
    {
        Assert.True(JournalRules.CanApprove(JournalEntryStatus.Submitted));
        Assert.False(JournalRules.CanApprove(JournalEntryStatus.Draft));
        Assert.True(JournalRules.CanPost(JournalEntryStatus.Approved));
        Assert.False(JournalRules.CanPost(JournalEntryStatus.Submitted));
        Assert.True(JournalRules.CanReverse(JournalEntryStatus.Posted));
        Assert.False(JournalRules.CanReverse(JournalEntryStatus.Reversed));
        Assert.True(JournalRules.CanReject(JournalEntryStatus.Approved));
        Assert.False(JournalRules.CanReject(JournalEntryStatus.Posted));
        Assert.True(JournalRules.IsInLedger(JournalEntryStatus.Reversed));
        Assert.False(JournalRules.IsInLedger(JournalEntryStatus.Approved));
    }

    // ------------------------------------------------------------------ account types

    [Theory]
    [InlineData(AccountType.Assets, NormalBalance.Debit, true)]
    [InlineData(AccountType.Liabilities, NormalBalance.Credit, true)]
    [InlineData(AccountType.Equity, NormalBalance.Credit, true)]
    [InlineData(AccountType.Revenue, NormalBalance.Credit, false)]
    [InlineData(AccountType.CostOfRevenue, NormalBalance.Debit, false)]
    [InlineData(AccountType.Expenses, NormalBalance.Debit, false)]
    public void AccountType_DecidesNormalBalanceAndStatement(AccountType type, NormalBalance side, bool balanceSheet)
    {
        Assert.Equal(side, AccountRules.NormalBalanceOf(type));
        Assert.Equal(balanceSheet, AccountRules.IsBalanceSheet(type));
        Assert.Equal(!balanceSheet, AccountRules.IsProfitAndLoss(type));
    }

    [Fact]
    public void NaturalBalance_AndColumns()
    {
        Assert.Equal(700m, AccountRules.NaturalBalance(AccountType.Assets, 1000m, 300m));
        Assert.Equal(700m, AccountRules.NaturalBalance(AccountType.Revenue, 300m, 1000m));
        Assert.Equal((250m, 0m), AccountRules.ToColumns(250m));
        Assert.Equal((0m, 250m), AccountRules.ToColumns(-250m));
        Assert.Equal(400m, AccountRules.ProfitEffect(100m, 500m));
    }

    // ------------------------------------------------------------------ base currency

    [Fact]
    public void ToBase_FixesTheRoundingDifference_SoBaseAmountsStayBalanced()
    {
        // 3 × 33.33 USD = 99.99 on each side; at 13,000.5 SYP / USD the rounded lines are 1 SYP apart.
        var lines = Lines((33.33m, 0m), (33.33m, 0m), (33.33m, 0m), (0m, 99.99m));

        var converted = JournalMath.ToBase(lines, 13_000.5m, 0);

        // Each debit line: 433,306.665 -> 433,307 (× 3 = 1,299,921); the credit 1,299,919.995 -> 1,299,920 is raised by 1.
        Assert.Equal(3 * JournalMath.Round(33.33m * 13_000.5m, 0), converted.Sum(x => x.Debit));
        Assert.Equal(converted.Sum(x => x.Debit), converted.Sum(x => x.Credit));
        Assert.Equal(1_299_921m, converted[3].Credit);
    }

    [Fact]
    public void ToBase_WithRateOne_KeepsTheAmounts()
    {
        var converted = JournalMath.ToBase(Lines((1500m, 0m), (0m, 1500m)), 1m, 0);

        Assert.Equal(1500m, converted[0].Debit);
        Assert.Equal(1500m, converted[1].Credit);
    }

    [Fact]
    public void ToBase_RefusesZeroRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => JournalMath.ToBase(Lines((1m, 0m), (0m, 1m)), 0m, 2));
    }

    // ------------------------------------------------------------------ periods

    [Fact]
    public void CalendarYear_HasTwelveMonthlyPeriods()
    {
        var periods = FiscalCalendar.MonthlyPeriods(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        Assert.Equal(12, periods.Count);
        Assert.Equal("كانون الثاني 2026", periods[0].Name);
        Assert.Equal(new DateTime(2026, 2, 28), periods[1].EndDate);
        Assert.Equal(new DateTime(2026, 12, 31), periods[^1].EndDate);
        Assert.All(periods.Zip(periods.Skip(1)), pair => Assert.Equal(pair.First.EndDate.AddDays(1), pair.Second.StartDate));
    }

    [Fact]
    public void YearStartingMidMonth_GetsAShortFirstPeriod()
    {
        var periods = FiscalCalendar.MonthlyPeriods(new DateTime(2026, 7, 15), new DateTime(2027, 7, 14));

        Assert.Equal(new DateTime(2026, 7, 15), periods[0].StartDate);
        Assert.Equal(new DateTime(2026, 7, 31), periods[0].EndDate);
        Assert.Equal(new DateTime(2027, 7, 14), periods[^1].EndDate);
        Assert.Equal(13, periods.Count);
    }

    // ------------------------------------------------------------------ posting rules

    [Fact]
    public void PostingRule_MostSpecificMatchWins_ThenPriority()
    {
        var rules = new List<AccountingPostingRule>
        {
            new() { Id = 1, AccountRole = AccountRoles.Expense, AccountId = 6209, IsActive = true },
            new() { Id = 2, AccountRole = AccountRoles.Expense, AccountId = 6101, ExpenseCategoryId = 7, IsActive = true },
            new() { Id = 3, AccountRole = AccountRoles.Expense, AccountId = 6102, ExpenseCategoryId = 7, BranchId = 2, IsActive = true },
            new() { Id = 4, AccountRole = AccountRoles.Expense, AccountId = 6103, ExpenseCategoryId = 7, Priority = 5, IsActive = true },
            new() { Id = 5, AccountRole = AccountRoles.CashOnHand, AccountId = 1101, IsActive = true }
        };

        Assert.Equal(6209, PostingRuleMatcher.Match(rules, new PostingScope(AccountRoles.Expense, BranchId: 1, ExpenseCategoryId: 9))!.AccountId);
        Assert.Equal(6103, PostingRuleMatcher.Match(rules, new PostingScope(AccountRoles.Expense, BranchId: 1, ExpenseCategoryId: 7))!.AccountId);
        Assert.Equal(6102, PostingRuleMatcher.Match(rules, new PostingScope(AccountRoles.Expense, BranchId: 2, ExpenseCategoryId: 7))!.AccountId);
        Assert.Equal(1101, PostingRuleMatcher.Match(rules, new PostingScope("cashonhand"))!.AccountId);
        Assert.Null(PostingRuleMatcher.Match(rules, new PostingScope(AccountRoles.Bank)));
    }

    [Fact]
    public void PostingRule_InactiveOrDeletedRules_AreIgnored()
    {
        var rules = new List<AccountingPostingRule>
        {
            new() { Id = 1, AccountRole = AccountRoles.Bank, AccountId = 1103, IsActive = false },
            new() { Id = 2, AccountRole = AccountRoles.Bank, AccountId = 1105, IsActive = true, IsDeleted = true }
        };

        Assert.Null(PostingRuleMatcher.Match(rules, new PostingScope(AccountRoles.Bank)));
    }

    // ------------------------------------------------------------------ posting requests of the operations

    [Fact]
    public void PostingRequest_RefusesUnbalancedLines_AndFlipsNegativeAmounts()
    {
        Assert.Throws<InvalidOperationException>(() => AccountingPostingRequest.Create(
            "Test", 1, "T-1", 1, DateTime.UtcNow, "x",
            new[] { AccountingPostingRequest.Debit(AccountRoles.CashOnHand, 100m), AccountingPostingRequest.Credit(AccountRoles.OtherIncome, 90m) }));

        var request = AccountingPostingRequest.Create(
            "Test", 1, "T-1", 1, DateTime.UtcNow, "x",
            new[]
            {
                AccountingPostingRequest.Debit(AccountRoles.CashOnHand, 100m),
                AccountingPostingRequest.Debit(AccountRoles.CashOverShort, -100m),
                AccountingPostingRequest.Debit(AccountRoles.Suspense, 0m)
            });

        Assert.Equal(2, request.Lines.Count);
        Assert.Equal(100m, request.Lines[1].Credit);
        Assert.Equal(request.TotalDebit, request.TotalCredit);

        var reversed = request.Reverse("عكس", DateTime.UtcNow);
        Assert.True(reversed.IsReversal);
        Assert.Equal(100m, reversed.Lines[0].Credit);
    }

    // ------------------------------------------------------------------ aging

    [Fact]
    public void Aging_PaymentsSettleTheOldestBillsFirst()
    {
        var asOf = new DateTime(2026, 10, 31);
        var result = OpenItemAging.Compute(new[]
        {
            new PartyMovement(new DateTime(2026, 6, 1), 1000m, 0m),
            new PartyMovement(new DateTime(2026, 9, 15), 500m, 0m),
            new PartyMovement(new DateTime(2026, 10, 20), 300m, 0m),
            new PartyMovement(new DateTime(2026, 10, 25), 0m, 1200m)
        }, asOf);

        Assert.Equal(0m, result.Over90Days);
        Assert.Equal(300m, result.Days31To60);
        Assert.Equal(300m, result.Days0To30);
        Assert.Equal(600m, result.TotalOpen);
        Assert.Equal(0m, result.Unapplied);
    }

    [Fact]
    public void Aging_PaymentBeforeAnyBill_IsAnAdvanceUsedByTheNextBill()
    {
        var result = OpenItemAging.Compute(new[]
        {
            new PartyMovement(new DateTime(2026, 1, 1), 0m, 800m),
            new PartyMovement(new DateTime(2026, 1, 5), 500m, 0m)
        }, new DateTime(2026, 1, 31));

        Assert.Equal(0m, result.TotalOpen);
        Assert.Equal(300m, result.Unapplied);
        Assert.Equal(-300m, result.NetBalance);
    }
}
