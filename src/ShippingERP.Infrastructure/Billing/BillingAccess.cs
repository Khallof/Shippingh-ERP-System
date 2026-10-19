using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Entities.Billing;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Billing;

public sealed record CurrencyRef(long Id, string Code, int DecimalPlaces);

/// <summary>
/// Branch scope of the receivables documents: users without Branches.ViewAll only see (and create) invoices,
/// payments, credit notes and sub-ledger rows of their own branch. A document outside the scope is reported as 404.
/// Also resolves the default branch and currency of new documents.
/// </summary>
public sealed class BillingAccess
{
    private readonly AppDbContext _dbContext;
    private readonly IBranchAccessService _branchAccess;
    private readonly ICurrentUserService _currentUser;

    private CurrencyRef? _baseCurrency;

    public BillingAccess(AppDbContext dbContext, IBranchAccessService branchAccess, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _branchAccess = branchAccess;
        _currentUser = currentUser;
    }

    public IBranchAccessService BranchAccess => _branchAccess;

    /// <summary>True when the caller sees every branch (or is a background job).</summary>
    public bool SeesAllBranches => !_currentUser.IsAuthenticated || _branchAccess.CanAccessAllBranches;

    public IQueryable<Invoice> Invoices(bool tracked = false)
    {
        IQueryable<Invoice> query = tracked ? _dbContext.Invoices : _dbContext.Invoices.AsNoTracking();
        if (SeesAllBranches)
        {
            return query;
        }

        var branchId = _branchAccess.CurrentBranchId;
        return query.Where(x => x.BranchId == branchId);
    }

    public IQueryable<CustomerPayment> Payments(bool tracked = false)
    {
        IQueryable<CustomerPayment> query = tracked ? _dbContext.CustomerPayments : _dbContext.CustomerPayments.AsNoTracking();
        if (SeesAllBranches)
        {
            return query;
        }

        var branchId = _branchAccess.CurrentBranchId;
        return query.Where(x => x.BranchId == branchId);
    }

    public IQueryable<CreditNote> CreditNotes()
    {
        var query = _dbContext.CreditNotes.AsNoTracking();
        if (SeesAllBranches)
        {
            return query;
        }

        var branchId = _branchAccess.CurrentBranchId;
        return query.Where(x => x.BranchId == branchId);
    }

    public IQueryable<CustomerAccountTransaction> Transactions()
    {
        var query = _dbContext.CustomerAccountTransactions.AsNoTracking();
        if (SeesAllBranches)
        {
            return query;
        }

        var branchId = _branchAccess.CurrentBranchId;
        return query.Where(x => x.BranchId == branchId);
    }

    /// <summary>
    /// The branch of a new document: the requested one, else the caller's branch, else <paramref name="fallback"/>.
    /// 403 when the caller may not use it, 400 when it does not exist or is inactive.
    /// </summary>
    public async Task<long> ResolveBranchAsync(long? requested, long? fallback, CancellationToken cancellationToken)
    {
        var branchId = requested ?? _currentUser.BranchId ?? fallback;
        if (branchId is null)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["branchId"] = new[] { "حدد الفرع." } });
        }

        _branchAccess.EnsureCanAccess(branchId.Value);

        var id = branchId.Value;
        var isActive = await _dbContext.Branches.AsNoTracking().Where(x => x.Id == id).Select(x => (bool?)x.IsActive).FirstOrDefaultAsync(cancellationToken);
        if (isActive is null)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["branchId"] = new[] { "الفرع غير موجود." } });
        }

        if (isActive == false)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["branchId"] = new[] { "الفرع غير مفعّل." } });
        }

        return id;
    }

    /// <summary>The requested currency, or the base currency. 400 when it does not exist.</summary>
    public async Task<CurrencyRef> GetCurrencyAsync(long? currencyId, CancellationToken cancellationToken)
    {
        if (currencyId is null)
        {
            return await GetBaseCurrencyAsync(cancellationToken);
        }

        var id = currencyId.Value;
        return await _dbContext.Currencies.AsNoTracking()
                   .Where(x => x.Id == id)
                   .Select(x => new CurrencyRef(x.Id, x.Code, x.DecimalPlaces))
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw new RequestValidationException(new Dictionary<string, string[]> { ["currencyId"] = new[] { "العملة غير موجودة." } });
    }

    public async Task<CurrencyRef> GetBaseCurrencyAsync(CancellationToken cancellationToken)
    {
        if (_baseCurrency is not null)
        {
            return _baseCurrency;
        }

        _baseCurrency = await _dbContext.Currencies.AsNoTracking()
                            .Where(x => x.IsBaseCurrency)
                            .Select(x => new CurrencyRef(x.Id, x.Code, x.DecimalPlaces))
                            .FirstOrDefaultAsync(cancellationToken)
                        ?? throw new BusinessRuleException("لا توجد عملة أساسية. عرّف العملة الأساسية في البيانات الأساسية أولاً.");
        return _baseCurrency;
    }

    public static decimal Round(decimal value, int decimals) =>
        Math.Round(value, Math.Clamp(decimals, 0, 4), MidpointRounding.AwayFromZero);

    /// <summary>True when SQL Server refused a duplicate key (unique index 2601 / unique constraint 2627).</summary>
    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627);
}
