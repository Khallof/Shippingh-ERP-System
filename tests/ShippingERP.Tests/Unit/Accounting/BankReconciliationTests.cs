using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;
using A = ShippingERP.Tests.Common.AccountingTestData;

namespace ShippingERP.Tests.Unit.Accounting;

/// <summary>
/// Bank reconciliation (step 7): automatic matching, statement-only lines (bank fees) that must be booked first,
/// completion with a zero difference, cleared items never matched twice.
/// Ledger: deposit 1,000,000 (capital) and rent paid 200,000 (reference RENT-1) on the demo bank account.
/// </summary>
public sealed class BankReconciliationTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DateTime _today;
    private long _bankAccountId;
    private long _banks;

    public async Task InitializeAsync()
    {
        await A.SeedAsync(_context);
        _today = await A.TodayAsync(_context);
        _bankAccountId = await CodTestData.BankAccountIdAsync(_context);
        _banks = await A.AccountIdAsync(_context, A.Banks);
        var capital = await A.AccountIdAsync(_context, A.Capital);
        var rent = await A.AccountIdAsync(_context, A.Rent);

        await A.PostManualAsync(_context, A.Entry(_today, "إيداع رأس المال",
            A.Debit(_banks, 1_000_000m, bankAccountId: _bankAccountId), A.Credit(capital, 1_000_000m)));

        var rentEntry = A.Entry(_today, "إيجار بتحويل بنكي", A.Debit(rent, 200_000m), A.Credit(_banks, 200_000m, bankAccountId: _bankAccountId));
        rentEntry.Reference = "RENT-1";
        await A.PostManualAsync(_context, rentEntry);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<BankReconciliationDto> Use(Func<IBankReconciliationService, Task<BankReconciliationDto>> action) => _context.UseAsync(action);

    private CreateBankReconciliationRequest Statement(decimal endingBalance, bool complete, params BankStatementLineRequest[] lines) => new()
    {
        BankAccountId = _bankAccountId,
        StatementDate = _today,
        StatementEndingBalance = endingBalance,
        StatementLines = lines.ToList(),
        AutoMatch = true,
        Complete = complete
    };

    private BankStatementLineRequest Line(string description, decimal deposit = 0m, decimal withdrawal = 0m, string? reference = null) =>
        new() { TransactionDate = _today, Description = description, Deposit = deposit, Withdrawal = withdrawal, Reference = reference };

    [Fact]
    public async Task MatchingStatement_CompletesAtOnce_AndClearsTheItems()
    {
        var result = await Use(s => s.CreateAsync(Statement(800_000m, complete: true,
            Line("إيداع نقدي", deposit: 1_000_000m),
            Line("حوالة صادرة", withdrawal: 200_000m, reference: "RENT-1")), CancellationToken.None));

        Assert.Equal(BankReconciliationStatus.Completed, result.Status);
        Assert.StartsWith("BRC-", result.ReconciliationNumber);
        Assert.Equal(800_000m, result.BookBalance);
        Assert.Equal(0m, result.Difference);
        Assert.Equal(2, result.MatchedCount);
        Assert.All(result.Lines, x => Assert.True(x.IsCleared));
        Assert.Equal(2, await _context.QueryAsync(db => db.BankTransactions.CountAsync(x => x.IsReconciled && x.MatchedJournalEntryLineId != null)));
    }

    [Fact]
    public async Task BankFee_OnlyOnTheStatement_KeepsItADraft_UntilItIsBooked()
    {
        var draft = await Use(s => s.CreateAsync(Statement(795_000m, complete: true,
            Line("إيداع نقدي", deposit: 1_000_000m),
            Line("حوالة صادرة", withdrawal: 200_000m, reference: "RENT-1"),
            Line("عمولة بنكية", withdrawal: 5_000m)), CancellationToken.None));

        Assert.Equal(BankReconciliationStatus.Draft, draft.Status);
        Assert.Equal(1, draft.BankOnlyCount);
        Assert.Equal(5_000m, draft.BankOnlyWithdrawals);
        Assert.Equal(0m, draft.Difference);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.CompleteAsync(draft.Id, new CompleteBankReconciliationRequest(), CancellationToken.None)));

        var charges = await A.AccountIdAsync(_context, A.BankCharges);
        await A.PostManualAsync(_context, A.Entry(_today, "عمولة بنكية من الكشف",
            A.Debit(charges, 5_000m), A.Credit(_banks, 5_000m, bankAccountId: _bankAccountId)));

        var completed = await Use(s => s.CompleteAsync(draft.Id, new CompleteBankReconciliationRequest { Notes = "سُجّلت العمولة" }, CancellationToken.None));

        Assert.Equal(BankReconciliationStatus.Completed, completed.Status);
        Assert.Equal(795_000m, completed.BookBalance);
        Assert.Equal(3, completed.MatchedCount);
        Assert.Equal(0, completed.BankOnlyCount);
    }

    [Fact]
    public async Task WrongStatementBalance_LeavesADifference_AndCannotBeCompleted()
    {
        var draft = await Use(s => s.CreateAsync(Statement(790_000m, complete: true,
            Line("إيداع نقدي", deposit: 1_000_000m),
            Line("حوالة صادرة", withdrawal: 200_000m, reference: "RENT-1")), CancellationToken.None));

        Assert.Equal(BankReconciliationStatus.Draft, draft.Status);
        Assert.Equal(-10_000m, draft.Difference);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.CompleteAsync(draft.Id, new CompleteBankReconciliationRequest(), CancellationToken.None)));
    }

    [Fact]
    public async Task UnpresentedWithdrawal_IsAnOutstandingItem_AndClearsInTheNextReconciliation()
    {
        // The bank has not paid the rent transfer yet: it is outstanding, the reconciliation still balances.
        var first = await Use(s => s.CreateAsync(Statement(1_000_000m, complete: true, Line("إيداع نقدي", deposit: 1_000_000m)), CancellationToken.None));

        Assert.Equal(BankReconciliationStatus.Completed, first.Status);
        Assert.Equal(200_000m, first.OutstandingWithdrawals);
        Assert.Equal(1, first.BookOnlyCount);

        var second = await Use(s => s.CreateAsync(Statement(800_000m, complete: true, Line("حوالة صادرة", withdrawal: 200_000m, reference: "RENT-1")), CancellationToken.None));

        Assert.Equal(BankReconciliationStatus.Completed, second.Status);
        Assert.Equal(1, second.MatchedCount);
        Assert.Equal(0, second.BookOnlyCount);
        Assert.Equal(0m, second.OutstandingWithdrawals);
    }

    [Fact]
    public async Task StatementBeforeTheLastCompletedReconciliation_IsRefused()
    {
        await Use(s => s.CreateAsync(Statement(800_000m, complete: true,
            Line("إيداع نقدي", deposit: 1_000_000m),
            Line("حوالة صادرة", withdrawal: 200_000m, reference: "RENT-1")), CancellationToken.None));

        var request = Statement(800_000m, complete: false);
        request.StatementDate = _today.AddDays(-1);

        var error = await Assert.ThrowsAsync<RequestValidationException>(() => Use(s => s.CreateAsync(request, CancellationToken.None)));
        Assert.Contains("statementDate", error.Errors.Keys);
    }
}
