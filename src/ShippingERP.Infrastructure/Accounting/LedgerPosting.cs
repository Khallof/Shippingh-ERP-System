using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Accounting;
using ShippingERP.Domain.Entities.Accounting;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Cash;
using ShippingERP.Infrastructure.Persistence;
using ShippingERP.Infrastructure.Shipments;

namespace ShippingERP.Infrastructure.Accounting;

/// <summary>
/// The journal posting domain service (step 7): every entry, manual or automatic, goes through it before it enters
/// the general ledger.
///
///   1. Period   : the entry date must be inside an OPEN period of an OPEN fiscal year. On SQL Server the period row
///                 is read WITH (HOLDLOCK, ROWLOCK) inside the posting transaction: a concurrent period closing
///                 (which updates that row) waits until the posting commits, and a posting that starts after the
///                 closing sees the Closed status. No entry can slip into a period while it is being closed.
///   2. Lines    : JournalRules (two lines or more, one side per line, debit = credit &gt; 0).
///   3. Accounts : exist, active, postable (not a header), currency restriction, cost center when required, and for
///                 manual entries AllowManualPosting (control accounts belong to the sub-ledgers).
///   4. Amounts  : base-currency amounts with the rounding difference fixed (JournalMath.ToBase), header totals.
///   5. Number   : JV-yyyy-nnnnnn from the MERGE ... HOLDLOCK counter (never a duplicate).
/// </summary>
public sealed class LedgerPosting
{
    private readonly AppDbContext _dbContext;
    private readonly DocumentNumberGenerator _numbers;
    private readonly AccountingAccess _access;

    public LedgerPosting(AppDbContext dbContext, DocumentNumberGenerator numbers, AccountingAccess access)
    {
        _dbContext = dbContext;
        _numbers = numbers;
        _access = access;
    }

    /// <summary>The accounting period containing <paramref name="date"/> (any status). 422 when none exists.</summary>
    public async Task<AccountingPeriod> FindPeriodAsync(DateTime date, CancellationToken cancellationToken)
    {
        var day = BusinessCalendar.AsDate(date);
        return await _dbContext.AccountingPeriods.AsNoTracking()
                   .Include(x => x.FiscalYear)
                   .FirstOrDefaultAsync(x => x.StartDate <= day && x.EndDate >= day, cancellationToken)
               ?? throw new BusinessRuleException(
                   $"لا توجد فترة محاسبية تشمل التاريخ {day:yyyy-MM-dd}. أنشئ السنة المالية من /api/v1/accounting/fiscal-years أولاً.");
    }

    /// <summary>
    /// The OPEN period of <paramref name="date"/>; 422 when it (or its fiscal year) is closed. Inside a SQL Server
    /// transaction the period row stays share-locked until the commit (see the class comment).
    /// </summary>
    public async Task<AccountingPeriod> EnsureOpenPeriodAsync(DateTime date, CancellationToken cancellationToken)
    {
        var period = await FindPeriodAsync(date, cancellationToken);
        var status = period.Status;

        if (_dbContext.Database.IsSqlServer() && _dbContext.Database.CurrentTransaction is not null)
        {
            var locked = await _dbContext.Database
                .SqlQuery<string>($"SELECT [Status] AS [Value] FROM [dbo].[AccountingPeriods] WITH (HOLDLOCK, ROWLOCK) WHERE [Id] = {period.Id}")
                .SingleAsync(cancellationToken);
            status = Enum.Parse<AccountingPeriodStatus>(locked);
        }

        if (status != AccountingPeriodStatus.Open || period.FiscalYear.Status != FiscalYearStatus.Open)
        {
            throw new BusinessRuleException(
                $"الفترة المحاسبية «{period.Name}» مقفلة؛ لا يمكن الترحيل بتاريخ {BusinessCalendar.AsDate(date):yyyy-MM-dd}. " +
                "استخدم تاريخاً في فترة مفتوحة أو أعد فتح الفترة.");
        }

        return period;
    }

    /// <summary>
    /// Checks the lines and their accounts, then fills the base amounts and the header totals (new or edited entries).
    /// </summary>
    public async Task PrepareAsync(JournalEntry entry, bool manual, CancellationToken cancellationToken)
    {
        await ValidateAsync(entry, manual, cancellationToken);
        ApplyAmounts(entry, entry.Lines.OrderBy(x => x.LineNumber).ToList(), (await _access.BaseCurrencyAsync(cancellationToken)).DecimalPlaces);
    }

    /// <summary>
    /// Checks the lines and their accounts WITHOUT changing anything (submit / post of a saved entry: its lines are
    /// immutable). Manual entries get a 400 with the field of each problem; automatic entries a 422 with all problems
    /// (the posting event shows it).
    /// </summary>
    public async Task ValidateAsync(JournalEntry entry, bool manual, CancellationToken cancellationToken)
    {
        var lines = entry.Lines.OrderBy(x => x.LineNumber).ToList();

        var balanceErrors = JournalRules.Validate(lines.Select(x => new JournalLineAmount(x.Debit, x.Credit)).ToList());
        if (balanceErrors.Count > 0)
        {
            throw manual
                ? new RequestValidationException(new Dictionary<string, string[]> { ["lines"] = balanceErrors.ToArray() })
                : new BusinessRuleException(string.Join(" ", balanceErrors));
        }

        var accountIds = lines.Select(x => x.AccountId).Distinct().ToList();
        var accounts = await _dbContext.Accounts.AsNoTracking()
            .Where(x => accountIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Code, x.ArabicName, x.IsActive, x.IsPostable, x.AllowManualPosting, x.CurrencyId, x.RequiresCostCenter })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var errors = new Dictionary<string, string[]>();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var key = string.Create(CultureInfo.InvariantCulture, $"lines[{i}].accountId");

            if (!accounts.TryGetValue(line.AccountId, out var account))
            {
                errors[key] = new[] { "الحساب غير موجود." };
                continue;
            }

            string? problem = null;
            if (!account.IsActive)
            {
                problem = $"الحساب {account.Code} غير مفعّل.";
            }
            else if (!account.IsPostable)
            {
                problem = $"الحساب {account.Code} حساب رئيسي (تجميعي) ولا يقبل القيود؛ اختر حساباً فرعياً.";
            }
            else if (manual && !account.AllowManualPosting)
            {
                problem = $"الحساب {account.Code} «{account.ArabicName}» حساب رقابي يُرحَّل آلياً من دفتره المساعد فقط.";
            }
            else if (account.CurrencyId.HasValue && account.CurrencyId != entry.CurrencyId)
            {
                problem = $"الحساب {account.Code} مقيّد بعملة أخرى غير عملة القيد.";
            }
            else if (account.RequiresCostCenter && !line.Dimensions.Any(d => d.DimensionType == DimensionType.CostCenter))
            {
                problem = $"الحساب {account.Code} يتطلب مركز تكلفة.";
            }

            if (problem is not null)
            {
                errors[key] = new[] { problem };
            }
        }

        if (errors.Count > 0)
        {
            throw manual
                ? new RequestValidationException(errors)
                : new BusinessRuleException(string.Join(" ", errors.Values.SelectMany(x => x)));
        }
    }

    /// <summary>Fills DebitBase / CreditBase (rounding fixed) and the four header totals.</summary>
    public static void ApplyAmounts(JournalEntry entry, IReadOnlyList<JournalEntryLine> orderedLines, int baseDecimals)
    {
        var baseAmounts = JournalMath.ToBase(
            orderedLines.Select(x => new JournalLineAmount(x.Debit, x.Credit)).ToList(), entry.ExchangeRate, baseDecimals);

        for (var i = 0; i < orderedLines.Count; i++)
        {
            orderedLines[i].DebitBase = baseAmounts[i].Debit;
            orderedLines[i].CreditBase = baseAmounts[i].Credit;
        }

        entry.TotalDebit = orderedLines.Sum(x => x.Debit);
        entry.TotalCredit = orderedLines.Sum(x => x.Credit);
        entry.TotalDebitBase = orderedLines.Sum(x => x.DebitBase);
        entry.TotalCreditBase = orderedLines.Sum(x => x.CreditBase);
    }

    /// <summary>
    /// The next JV number. Call it BEFORE adding or changing tracked entities: on the in-memory test provider the
    /// generator saves the context.
    /// </summary>
    public Task<string> NextNumberAsync(CancellationToken cancellationToken) =>
        _numbers.NextAsync(DocumentPrefixes.JournalEntry, cancellationToken);

    /// <summary>The next DRF number of a manual draft (same rule as <see cref="NextNumberAsync"/>).</summary>
    public Task<string> NextDraftNumberAsync(CancellationToken cancellationToken) =>
        _numbers.NextAsync(DocumentPrefixes.DraftJournalEntry, cancellationToken);

    public void MarkPosted(JournalEntry entry)
    {
        entry.Status = JournalEntryStatus.Posted;
        entry.PostedAtUtc = _access.UtcNow;
        entry.PostedByUserId = _access.CurrentUser.UserId;
    }

    /// <summary>
    /// Builds the reversal of a posted entry (debits and credits swapped, the same base amounts so it cancels it
    /// exactly), marks the original Reversed and links both. The caller saves inside its transaction, after
    /// <see cref="EnsureOpenPeriodAsync"/> for the reversal date.
    /// </summary>
    public JournalEntry CreateReversal(
        JournalEntry original,
        AccountingPeriod period,
        DateTime reversalDate,
        string entryNumber,
        string reason,
        bool automatic,
        string? sourceKey = null,
        string? sourceEventType = null)
    {
        if (!JournalRules.CanReverse(original.Status))
        {
            throw new BusinessRuleException($"القيد {original.EntryNumber} في حالة «{AccountingNames.Arabic(original.Status)}»؛ يُعكس القيد المرحّل فقط.");
        }

        var now = _access.UtcNow;
        var userId = _access.CurrentUser.UserId;

        var reversal = new JournalEntry
        {
            EntryNumber = entryNumber,
            JournalType = JournalType.Reversal,
            Status = JournalEntryStatus.Posted,
            EntryDate = BusinessCalendar.AsDate(reversalDate),
            AccountingPeriodId = period.Id,
            BranchId = original.BranchId,
            CurrencyId = original.CurrencyId,
            ExchangeRate = original.ExchangeRate,
            Description = Clip($"عكس القيد {original.EntryNumber}: {reason}", 500),
            Reference = original.EntryNumber,
            TotalDebit = original.TotalCredit,
            TotalCredit = original.TotalDebit,
            TotalDebitBase = original.TotalCreditBase,
            TotalCreditBase = original.TotalDebitBase,
            IsSystemGenerated = automatic || original.IsSystemGenerated,
            SourceType = original.SourceType,
            SourceId = original.SourceId,
            SourceNumber = original.SourceNumber,
            SourceEventType = sourceEventType ?? (automatic ? original.SourceEventType : "ManualReversal"),
            SourceKey = sourceKey,
            PostedAtUtc = now,
            PostedByUserId = userId,
            ReversalOfEntryId = original.Id
        };

        foreach (var line in original.Lines.OrderBy(x => x.LineNumber))
        {
            var copy = new JournalEntryLine
            {
                LineNumber = line.LineNumber,
                AccountId = line.AccountId,
                Description = line.Description,
                Debit = line.Credit,
                Credit = line.Debit,
                DebitBase = line.CreditBase,
                CreditBase = line.DebitBase,
                BranchId = line.BranchId,
                AccountRole = line.AccountRole,
                CustomerId = line.CustomerId,
                SupplierId = line.SupplierId,
                DriverId = line.DriverId,
                EmployeeId = line.EmployeeId,
                CashboxId = line.CashboxId,
                BankAccountId = line.BankAccountId,
                ExpenseCategoryId = line.ExpenseCategoryId
            };

            foreach (var dimension in line.Dimensions)
            {
                copy.Dimensions.Add(new JournalEntryDimension { DimensionType = dimension.DimensionType, AccountingDimensionId = dimension.AccountingDimensionId });
            }

            reversal.Lines.Add(copy);
        }

        original.Status = JournalEntryStatus.Reversed;
        original.ReversedAtUtc = now;
        original.ReversedByUserId = userId;
        original.ReversedByEntry = reversal;

        _dbContext.JournalEntries.Add(reversal);
        _dbContext.ReversalJournalLinks.Add(new ReversalJournalLink
        {
            OriginalEntry = original,
            ReversalEntry = reversal,
            Reason = Clip(reason, 500),
            IsAutomatic = automatic
        });

        return reversal;
    }

    public static string Clip(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
