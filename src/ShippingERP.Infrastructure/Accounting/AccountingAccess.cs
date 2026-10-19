using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Entities.Accounting;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.Cash;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Accounting;

/// <summary>
/// Shared helpers of the accounting services (step 7): branch visibility, four-eyes approval, base currency,
/// exchange rates, the head-office branch and the business calendar.
///
/// Visibility: the general ledger is company wide. A user without Branches.ViewAll sees only the journal entries of
/// his branch (header or any line) and every report is forced to his branch.
/// </summary>
public sealed class AccountingAccess
{
    private readonly AppDbContext _dbContext;
    private readonly IBranchAccessService _branchAccess;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly AccountingOptions _options;
    private long? _defaultBranchId;

    public AccountingAccess(
        AppDbContext dbContext,
        IBranchAccessService branchAccess,
        ICurrentUserService currentUser,
        IDateTimeProvider clock,
        BillingAccess billing,
        BusinessCalendar calendar,
        IOptions<AccountingOptions> options)
    {
        _dbContext = dbContext;
        _branchAccess = branchAccess;
        _currentUser = currentUser;
        _clock = clock;
        Billing = billing;
        Calendar = calendar;
        _options = options.Value;
    }

    public BillingAccess Billing { get; }

    public AccountingOptions Options => _options;

    public BusinessCalendar Calendar { get; }

    public ICurrentUserService CurrentUser => _currentUser;

    public IBranchAccessService BranchAccess => _branchAccess;

    public DateTime UtcNow => _clock.UtcNow;

    /// <summary>Today's local business date.</summary>
    public DateTime Today => Calendar.BusinessDate(_clock.UtcNow);

    public bool SeesAllBranches => !_currentUser.IsAuthenticated || _branchAccess.CanAccessAllBranches;

    /// <summary>The branch every report of this user is limited to (null = all branches; -1 = none).</summary>
    public long? ForcedBranchId => SeesAllBranches ? null : (_branchAccess.CurrentBranchId ?? -1);

    /// <summary>The branch filter of a report: the requested branch (if allowed) or the forced one.</summary>
    public long? ReportBranch(long? requested)
    {
        if (requested.HasValue)
        {
            _branchAccess.EnsureCanAccess(requested.Value);
            return requested.Value;
        }

        return ForcedBranchId;
    }

    /// <summary>Journal entries the caller may see.</summary>
    public IQueryable<JournalEntry> Entries(bool tracked = false)
    {
        IQueryable<JournalEntry> query = tracked ? _dbContext.JournalEntries : _dbContext.JournalEntries.AsNoTracking();
        var branchId = ForcedBranchId;
        return branchId is null ? query : query.Where(x => x.BranchId == branchId || x.Lines.Any(l => l.BranchId == branchId));
    }

    /// <summary>422 when the caller approves his own entry (four-eyes rule).</summary>
    public void EnsureOtherApprover(long? authorUserId, string what)
    {
        if (_options.AllowSelfApproval || !_currentUser.IsAuthenticated || authorUserId is null)
        {
            return;
        }

        if (authorUserId == _currentUser.UserId)
        {
            throw new BusinessRuleException($"لا يمكنك اعتماد {what} بنفسك: يجب أن يعتمده مستخدم آخر غير مُعدّه.");
        }
    }

    /// <summary>403 when a signed-in caller lacks an extra permission.</summary>
    public void EnsurePermission(string permission, string message)
    {
        if (_currentUser.IsAuthenticated && !_currentUser.HasPermission(permission))
        {
            throw new ForbiddenException(message);
        }
    }

    public static RequestValidationException Invalid(string fieldName, string message) =>
        new(new Dictionary<string, string[]> { [fieldName] = new[] { message } });

    public Task<CurrencyRef> BaseCurrencyAsync(CancellationToken cancellationToken) => Billing.GetBaseCurrencyAsync(cancellationToken);

    /// <summary>The head office (or the first active branch): the branch of lines that have none.</summary>
    public async Task<long> DefaultBranchIdAsync(CancellationToken cancellationToken)
    {
        if (_defaultBranchId.HasValue)
        {
            return _defaultBranchId.Value;
        }

        var id = await _dbContext.Branches.AsNoTracking()
                     .OrderByDescending(x => x.IsHeadOffice)
                     .ThenByDescending(x => x.IsActive)
                     .ThenBy(x => x.Id)
                     .Select(x => (long?)x.Id)
                     .FirstOrDefaultAsync(cancellationToken)
                 ?? throw new BusinessRuleException("لا يوجد أي فرع؛ عرّف الفروع أولاً.");

        _defaultBranchId = id;
        return id;
    }

    /// <summary>
    /// Base currency units for one unit of <paramref name="currencyId"/> at <paramref name="date"/>: the latest rate
    /// currency -> base effective on that day, else the inverse of base -> currency. 1 for the base currency.
    /// </summary>
    public async Task<decimal> ExchangeRateAsync(long currencyId, DateTime date, CancellationToken cancellationToken)
    {
        var baseCurrency = await BaseCurrencyAsync(cancellationToken);
        if (currencyId == baseCurrency.Id)
        {
            return 1m;
        }

        var until = Calendar.DayRangeUtc(BusinessCalendar.AsDate(date)).EndUtc;

        var direct = await _dbContext.ExchangeRates.AsNoTracking()
            .Where(x => x.IsActive && x.FromCurrencyId == currencyId && x.ToCurrencyId == baseCurrency.Id && x.EffectiveFromUtc < until)
            .OrderByDescending(x => x.EffectiveFromUtc)
            .Select(x => (decimal?)x.Rate)
            .FirstOrDefaultAsync(cancellationToken);

        if (direct is > 0m)
        {
            return direct.Value;
        }

        var inverse = await _dbContext.ExchangeRates.AsNoTracking()
            .Where(x => x.IsActive && x.FromCurrencyId == baseCurrency.Id && x.ToCurrencyId == currencyId && x.EffectiveFromUtc < until)
            .OrderByDescending(x => x.EffectiveFromUtc)
            .Select(x => (decimal?)x.Rate)
            .FirstOrDefaultAsync(cancellationToken);

        if (inverse is > 0m)
        {
            return Math.Round(1m / inverse.Value, 8, MidpointRounding.AwayFromZero);
        }

        var code = await _dbContext.Currencies.AsNoTracking().Where(x => x.Id == currencyId).Select(x => x.Code).FirstOrDefaultAsync(cancellationToken);
        throw new BusinessRuleException($"لا يوجد سعر صرف للعملة {code ?? currencyId.ToString()} مقابل العملة الأساسية بتاريخ {date:yyyy-MM-dd}.");
    }

    /// <summary>The first day of the fiscal year containing <paramref name="date"/> (1 January when there is none).</summary>
    public async Task<DateTime> FiscalYearStartAsync(DateTime date, CancellationToken cancellationToken)
    {
        var day = BusinessCalendar.AsDate(date);
        var start = await _dbContext.FiscalYears.AsNoTracking()
            .Where(x => x.StartDate <= day && x.EndDate >= day)
            .Select(x => (DateTime?)x.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        return start ?? BusinessCalendar.AsDate(new DateTime(day.Year, 1, 1));
    }
}
