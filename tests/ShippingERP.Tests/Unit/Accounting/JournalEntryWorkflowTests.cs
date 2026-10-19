using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;
using A = ShippingERP.Tests.Common.AccountingTestData;

namespace ShippingERP.Tests.Unit.Accounting;

/// <summary>
/// Manual journal entries (step 7): balance checks, the Draft -> Submitted -> Approved -> Posted workflow with four
/// eyes, rejection, immutability of posted entries (service, interceptor), reversals and the Idempotency-Key.
/// </summary>
public sealed class JournalEntryWorkflowTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private DateTime _today;
    private long _rent;
    private long _banks;

    public async Task InitializeAsync()
    {
        await A.SeedAsync(_context);
        _today = await A.TodayAsync(_context);
        _rent = await A.AccountIdAsync(_context, A.Rent);
        _banks = await A.AccountIdAsync(_context, A.Banks);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private Task<T> Use<T>(Func<IJournalEntryService, Task<T>> action) => _context.UseAsync(action);

    private SaveJournalEntryRequest RentEntry(decimal amount = 1_200_000m) =>
        A.Entry(_today, "إيجار المكتب - تحويل بنكي", A.Debit(_rent, amount), A.Credit(_banks, amount));

    private async Task<JournalEntryDto> SubmittedAsync(long authorId = 801, SaveJournalEntryRequest? request = null)
    {
        _context.CurrentUser.SignInAsSystemAdmin(authorId);
        request ??= RentEntry();
        request.Submit = true;
        return (await A.CreateAsync(_context, request)).Entry;
    }

    [Fact]
    public async Task NewEntry_IsADraft_WithADraftNumber_AndStaysOutOfTheLedger()
    {
        var created = await A.CreateAsync(_context, RentEntry());

        Assert.False(created.Replayed);
        var entry = created.Entry;
        Assert.Equal(JournalEntryStatus.Draft, entry.Status);
        Assert.StartsWith("DRF-", entry.EntryNumber);
        Assert.Equal(1_200_000m, entry.TotalDebit);
        Assert.Equal(entry.TotalDebit, entry.TotalCredit);
        Assert.Equal(1_200_000m, entry.TotalDebitBase);
        Assert.True(entry.IsBalanced);
        Assert.Equal(2, entry.Lines.Count);
        Assert.Equal(0m, await A.BalanceAsync(_context, A.Rent));
    }

    [Fact]
    public async Task UnbalancedEntry_IsRejectedWith400()
    {
        var request = A.Entry(_today, "قيد غير متوازن", A.Debit(_rent, 1000m), A.Credit(_banks, 999m));

        var error = await Assert.ThrowsAsync<RequestValidationException>(() => A.CreateAsync(_context, request));

        Assert.Contains("lines", error.Errors.Keys);
        Assert.Equal(0, await _context.QueryAsync(db => db.JournalEntries.CountAsync()));
    }

    [Fact]
    public async Task HeaderAccount_AndControlAccounts_RefuseManualLines()
    {
        var header = await A.AccountIdAsync(_context, "61");
        var headerError = await Assert.ThrowsAsync<RequestValidationException>(() =>
            A.CreateAsync(_context, A.Entry(_today, "حساب رئيسي", A.Debit(header, 500m), A.Credit(_banks, 500m))));
        Assert.Contains("lines[0].accountId", headerError.Errors.Keys);

        // The cashbox account belongs to the cash sub-ledger: only the automatic postings may use it.
        var cashboxes = await A.AccountIdAsync(_context, A.Cashboxes);
        var controlError = await Assert.ThrowsAsync<RequestValidationException>(() =>
            A.CreateAsync(_context, A.Entry(_today, "حساب رقابي", A.Debit(_rent, 500m), A.Credit(cashboxes, 500m))));
        Assert.Contains("lines[1].accountId", controlError.Errors.Keys);
    }

    [Fact]
    public async Task FullWorkflow_PostsWithAJournalNumber_AndUpdatesTheLedger()
    {
        var posted = await A.PostManualAsync(_context, RentEntry());

        Assert.Equal(JournalEntryStatus.Posted, posted.Status);
        Assert.StartsWith($"JV-{_today.Year}-", posted.EntryNumber);
        Assert.Equal(801, posted.SubmittedByUserId);
        Assert.Equal(802, posted.ApprovedByUserId);
        Assert.Equal(802, posted.PostedByUserId);
        Assert.NotNull(posted.PostedAtUtc);
        Assert.Equal(1_200_000m, await A.BalanceAsync(_context, A.Rent));
        Assert.Equal(-1_200_000m, await A.BalanceAsync(_context, A.Banks));
        Assert.Empty(await A.UnbalancedEntriesAsync(_context));
    }

    [Fact]
    public async Task Author_CannotApproveHisOwnEntry()
    {
        var submitted = await SubmittedAsync(801);

        _context.CurrentUser.SignInAsSystemAdmin(801);
        await Assert.ThrowsAsync<BusinessRuleException>(() => A.ApproveAsync(_context, submitted.Id));

        _context.CurrentUser.SignInAsSystemAdmin(802);
        var approved = await A.ApproveAsync(_context, submitted.Id);
        Assert.Equal(JournalEntryStatus.Approved, approved.Status);
    }

    [Fact]
    public async Task OnlyApprovedEntries_ArePosted()
    {
        var draft = (await A.CreateAsync(_context, RentEntry())).Entry;
        await Assert.ThrowsAsync<BusinessRuleException>(() => A.PostAsync(_context, draft.Id));

        var submitted = await SubmittedAsync();
        _context.CurrentUser.SignInAsSystemAdmin(802);
        await Assert.ThrowsAsync<BusinessRuleException>(() => A.PostAsync(_context, submitted.Id));
        Assert.Equal(0m, await A.BalanceAsync(_context, A.Rent));
    }

    [Fact]
    public async Task RejectedEntry_CanBeCorrectedAndSubmittedAgain()
    {
        var submitted = await SubmittedAsync(801);

        _context.CurrentUser.SignInAsSystemAdmin(802);
        var rejected = await Use(s => s.RejectAsync(submitted.Id, new JournalReasonRequest { Reason = "المبلغ لا يطابق عقد الإيجار" }, CancellationToken.None));
        Assert.Equal(JournalEntryStatus.Rejected, rejected.Status);
        Assert.Equal("المبلغ لا يطابق عقد الإيجار", rejected.RejectionReason);

        _context.CurrentUser.SignInAsSystemAdmin(801);
        var edited = await Use(s => s.UpdateAsync(submitted.Id, RentEntry(1_000_000m), CancellationToken.None));
        Assert.Equal(JournalEntryStatus.Draft, edited.Status);
        Assert.Equal(1_000_000m, edited.TotalDebit);
        Assert.Equal(2, edited.Lines.Count);

        var resubmitted = await A.SubmitAsync(_context, submitted.Id);
        Assert.Equal(JournalEntryStatus.Submitted, resubmitted.Status);
        Assert.Null(resubmitted.RejectionReason);
    }

    [Fact]
    public async Task PostedEntry_CannotBeEditedOrDeleted()
    {
        var posted = await A.PostManualAsync(_context, RentEntry());

        await Assert.ThrowsAsync<BusinessRuleException>(() => Use(s => s.UpdateAsync(posted.Id, RentEntry(5m), CancellationToken.None)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Use<bool>(async s =>
        {
            await s.DeleteAsync(posted.Id, CancellationToken.None);
            return true;
        }));

        var reloaded = await A.GetAsync(_context, posted.Id);
        Assert.Equal(JournalEntryStatus.Posted, reloaded.Status);
        Assert.Equal(1_200_000m, reloaded.TotalDebit);
    }

    [Fact]
    public async Task Interceptor_BlocksAnyDirectChangeOfAPostedEntryOrItsLines()
    {
        var posted = await A.PostManualAsync(_context, RentEntry());

        await Assert.ThrowsAsync<InvalidOperationException>(() => _context.QueryAsync(async db =>
        {
            var entry = await db.JournalEntries.SingleAsync(x => x.Id == posted.Id);
            entry.Description = "تعديل مباشر";
            return await db.SaveChangesAsync();
        }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _context.QueryAsync(async db =>
        {
            var line = await db.JournalEntryLines.FirstAsync(x => x.JournalEntryId == posted.Id);
            line.Debit += 1m;
            return await db.SaveChangesAsync();
        }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _context.QueryAsync(async db =>
        {
            var entry = await db.JournalEntries.Include(x => x.Lines).SingleAsync(x => x.Id == posted.Id);
            db.JournalEntryLines.RemoveRange(entry.Lines);
            db.JournalEntries.Remove(entry);
            return await db.SaveChangesAsync();
        }));

        Assert.Equal(1_200_000m, await A.BalanceAsync(_context, A.Rent));
    }

    [Fact]
    public async Task Reverse_PostsTheOppositeEntry_AndTheLedgerNetsToZero()
    {
        var posted = await A.PostManualAsync(_context, RentEntry());

        _context.CurrentUser.SignInAsSystemAdmin(803);
        var reversal = await A.ReverseAsync(_context, posted.Id, "قيد الإيجار مكرر");

        Assert.Equal(JournalType.Reversal, reversal.JournalType);
        Assert.Equal(JournalEntryStatus.Posted, reversal.Status);
        Assert.StartsWith("JV-", reversal.EntryNumber);
        Assert.Equal(posted.Id, reversal.ReversalOfEntryId);
        Assert.Equal(posted.EntryNumber, reversal.ReversalOfEntryNumber);
        Assert.Equal(posted.Lines.Single(x => x.AccountId == _rent).Debit, reversal.Lines.Single(x => x.AccountId == _rent).Credit);
        Assert.Equal(posted.Lines.Single(x => x.AccountId == _banks).Credit, reversal.Lines.Single(x => x.AccountId == _banks).Debit);

        var original = await A.GetAsync(_context, posted.Id);
        Assert.Equal(JournalEntryStatus.Reversed, original.Status);
        Assert.Equal(reversal.Id, original.ReversedByEntryId);
        Assert.Equal(803, original.ReversedByUserId);

        Assert.Equal(0m, await A.BalanceAsync(_context, A.Rent));
        Assert.Equal(0m, await A.BalanceAsync(_context, A.Banks));
        Assert.Equal(1, await _context.QueryAsync(db => db.ReversalJournalLinks.CountAsync(x => x.OriginalEntryId == posted.Id && x.ReversalEntryId == reversal.Id)));
    }

    [Fact]
    public async Task Entry_IsReversedOnceOnly_AndAReversalIsNeverReversed()
    {
        var posted = await A.PostManualAsync(_context, RentEntry());
        var reversal = await A.ReverseAsync(_context, posted.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => A.ReverseAsync(_context, posted.Id));
        await Assert.ThrowsAsync<BusinessRuleException>(() => A.ReverseAsync(_context, reversal.Id));
        Assert.Equal(2, await _context.QueryAsync(db => db.JournalEntries.CountAsync()));
    }

    [Fact]
    public async Task Reversal_CannotBeDatedBeforeTheOriginal()
    {
        var posted = await A.PostManualAsync(_context, RentEntry());

        var error = await Assert.ThrowsAsync<RequestValidationException>(() => A.ReverseAsync(_context, posted.Id, date: _today.AddDays(-1)));
        Assert.Contains("reversalDate", error.Errors.Keys);
    }

    [Fact]
    public async Task AutomaticEntry_IsNotReversedByHand()
    {
        await A.ProcessAsync(_context);
        var automatic = (await A.EntriesOfAsync(_context, "CashboxOpeningBalance")).First();

        await Assert.ThrowsAsync<BusinessRuleException>(() => A.ReverseAsync(_context, automatic.Id));
    }

    [Fact]
    public async Task IdempotencyKey_ReplaysTheFirstEntry_AndRefusesAnotherBody()
    {
        _context.CurrentUser.SignInAsSystemAdmin(801);

        var first = await A.CreateAsync(_context, RentEntry(), "rent-oct-2026");
        var replay = await A.CreateAsync(_context, RentEntry(), "rent-oct-2026");

        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(first.Entry.Id, replay.Entry.Id);
        Assert.Equal(1, await _context.QueryAsync(db => db.JournalEntries.CountAsync(x => !x.IsSystemGenerated)));

        await Assert.ThrowsAsync<ConflictException>(() => A.CreateAsync(_context, RentEntry(999m), "rent-oct-2026"));
    }

    [Fact]
    public async Task CostCenter_IsKeptOnTheLine()
    {
        var admin = await A.CostCenterIdAsync(_context, "ADMIN");

        var entry = (await A.CreateAsync(_context, A.Entry(_today, "إيجار", A.Debit(_rent, 300m, costCenterId: admin), A.Credit(_banks, 300m)))).Entry;

        var dimension = Assert.Single(entry.Lines.Single(x => x.AccountId == _rent).Dimensions);
        Assert.Equal(DimensionType.CostCenter, dimension.DimensionType);
        Assert.Equal("ADMIN", dimension.Code);
    }

    [Fact]
    public async Task ForeignCurrencyEntry_IsConvertedToTheBaseCurrency()
    {
        var usd = await _context.QueryAsync(db => db.Currencies.Where(x => x.Code == "USD").Select(x => x.Id).SingleAsync());
        var request = RentEntry(100m);
        request.CurrencyId = usd;
        request.ExchangeRate = 13_000m;

        var posted = await A.PostManualAsync(_context, request);

        Assert.Equal(100m, posted.TotalDebit);
        Assert.Equal(1_300_000m, posted.TotalDebitBase);
        Assert.Equal(posted.TotalDebitBase, posted.TotalCreditBase);
        Assert.Equal(1_300_000m, await A.BalanceAsync(_context, A.Rent));
    }

    [Fact]
    public async Task ManualEntries_UseManualJournalTypesOnly()
    {
        var validator = new ShippingERP.Application.Validators.Accounting.SaveJournalEntryRequestValidator();
        var request = RentEntry();
        request.JournalType = JournalType.Sales;

        var result = await validator.ValidateAsync(request);

        // Only the journal type is wrong (the error key itself depends on the global camelCase name resolver).
        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Contains("القيود اليدوية", error.ErrorMessage);
        request.JournalType = JournalType.Adjustment;
        Assert.True((await validator.ValidateAsync(request)).IsValid);
    }
}
