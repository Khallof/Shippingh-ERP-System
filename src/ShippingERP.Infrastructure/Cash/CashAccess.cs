using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Entities.Cash;
using ShippingERP.Domain.Entities.Cod;
using ShippingERP.Infrastructure.Billing;
using ShippingERP.Infrastructure.Persistence;
using ShippingERP.Infrastructure.Shipments;

namespace ShippingERP.Infrastructure.Cash;

/// <summary>
/// Visibility and approval rules of the COD and cash documents (step 6).
///
/// - Not signed in (seeding, background jobs) or Branches.ViewAll: everything.
/// - Driver-only user (Driver role without Shipments.AssignDriver): only his own COD records and cash account.
/// - Other users: their branch only. A COD belongs to the branches of its shipment (origin, current, destination)
///   and to the branch of the cashbox that received it; transfers belong to both their source and destination.
/// Documents outside the scope are reported as 404, never 403.
///
/// Approvals ("four eyes"): the approver must be another user than the requester, unless
/// Cash:AllowSelfApproval = true.
/// </summary>
public sealed class CashAccess
{
    private readonly AppDbContext _dbContext;
    private readonly IBranchAccessService _branchAccess;
    private readonly ICurrentUserService _currentUser;
    private readonly ShipmentAccessService _shipmentAccess;
    private readonly CashOptions _options;

    public CashAccess(
        AppDbContext dbContext,
        IBranchAccessService branchAccess,
        ICurrentUserService currentUser,
        ShipmentAccessService shipmentAccess,
        BillingAccess billing,
        IOptions<CashOptions> options)
    {
        _dbContext = dbContext;
        _branchAccess = branchAccess;
        _currentUser = currentUser;
        _shipmentAccess = shipmentAccess;
        Billing = billing;
        _options = options.Value;
    }

    /// <summary>Branch / currency helpers shared with the billing services.</summary>
    public BillingAccess Billing { get; }

    public IBranchAccessService BranchAccess => _branchAccess;

    public ICurrentUserService CurrentUser => _currentUser;

    public bool SeesAllBranches => !_currentUser.IsAuthenticated || _branchAccess.CanAccessAllBranches;

    public bool IsDriverOnly => _shipmentAccess.IsDriverOnly;

    private long? BranchId => _branchAccess.CurrentBranchId;

    public Task<long?> CurrentDriverIdAsync(CancellationToken cancellationToken) => _shipmentAccess.GetCurrentDriverIdAsync(cancellationToken);

    // ------------------------------------------------------------------ scopes

    public async Task<IQueryable<CODTransaction>> CodsAsync(bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<CODTransaction> query = tracked ? _dbContext.CODTransactions : _dbContext.CODTransactions.AsNoTracking();

        if (IsDriverOnly)
        {
            var driverId = await CurrentDriverIdAsync(cancellationToken);
            return driverId is null ? query.Where(_ => false) : query.Where(x => x.DriverId == driverId);
        }

        if (SeesAllBranches)
        {
            return query;
        }

        var branchId = BranchId;
        return query.Where(x =>
            x.OriginBranchId == branchId ||
            x.Shipment.CurrentBranchId == branchId ||
            x.Shipment.DestinationBranchId == branchId ||
            (x.ReceivedCashboxId != null && x.ReceivedCashbox!.BranchId == branchId));
    }

    public async Task<IQueryable<DriverCashAccount>> DriverAccountsAsync(CancellationToken cancellationToken)
    {
        var query = _dbContext.DriverCashAccounts.AsNoTracking();

        if (IsDriverOnly)
        {
            var driverId = await CurrentDriverIdAsync(cancellationToken);
            return driverId is null ? query.Where(_ => false) : query.Where(x => x.DriverId == driverId);
        }

        if (SeesAllBranches)
        {
            return query;
        }

        var branchId = BranchId;
        return query.Where(x => x.Driver.Employee.BranchId == branchId);
    }

    public IQueryable<Cashbox> Cashboxes(bool tracked = false)
    {
        IQueryable<Cashbox> query = tracked ? _dbContext.Cashboxes : _dbContext.Cashboxes.AsNoTracking();
        if (SeesAllBranches)
        {
            return query;
        }

        var branchId = BranchId;
        return query.Where(x => x.BranchId == branchId);
    }

    public IQueryable<CashReceiptVoucher> Receipts(bool tracked = false)
    {
        IQueryable<CashReceiptVoucher> query = tracked ? _dbContext.CashReceiptVouchers : _dbContext.CashReceiptVouchers.AsNoTracking();
        if (SeesAllBranches)
        {
            return query;
        }

        var branchId = BranchId;
        return query.Where(x => x.BranchId == branchId);
    }

    public IQueryable<CashPaymentVoucher> Payments(bool tracked = false)
    {
        IQueryable<CashPaymentVoucher> query = tracked ? _dbContext.CashPaymentVouchers : _dbContext.CashPaymentVouchers.AsNoTracking();
        if (SeesAllBranches)
        {
            return query;
        }

        var branchId = BranchId;
        return query.Where(x => x.BranchId == branchId);
    }

    public IQueryable<CashTransfer> Transfers(bool tracked = false)
    {
        IQueryable<CashTransfer> query = tracked ? _dbContext.CashTransfers : _dbContext.CashTransfers.AsNoTracking();
        if (SeesAllBranches)
        {
            return query;
        }

        var branchId = BranchId;
        return query.Where(x => x.FromBranchId == branchId || x.ToBranchId == branchId);
    }

    public IQueryable<CashboxDailyClosing> Closings(bool tracked = false)
    {
        IQueryable<CashboxDailyClosing> query = tracked ? _dbContext.CashboxDailyClosings : _dbContext.CashboxDailyClosings.AsNoTracking();
        if (SeesAllBranches)
        {
            return query;
        }

        var branchId = BranchId;
        return query.Where(x => x.Cashbox.BranchId == branchId);
    }

    public IQueryable<SenderSettlement> Settlements(bool tracked = false)
    {
        IQueryable<SenderSettlement> query = tracked ? _dbContext.SenderSettlements : _dbContext.SenderSettlements.AsNoTracking();
        if (SeesAllBranches)
        {
            return query;
        }

        var branchId = BranchId;
        return query.Where(x => x.BranchId == branchId);
    }

    public IQueryable<CODAdjustment> Adjustments(bool tracked = false)
    {
        IQueryable<CODAdjustment> query = tracked ? _dbContext.CODAdjustments : _dbContext.CODAdjustments.AsNoTracking();
        if (SeesAllBranches)
        {
            return query;
        }

        var branchId = BranchId;
        return query.Where(x => x.BranchId == branchId);
    }

    // ------------------------------------------------------------------ loading for money operations

    /// <summary>
    /// A cashbox the caller may move money with (tracked): visible, active and, when given, in the expected
    /// currency. 400 with the field name otherwise.
    /// </summary>
    public async Task<Cashbox> CashboxForPostingAsync(long cashboxId, long? expectedCurrencyId, string fieldName, CancellationToken cancellationToken)
    {
        var cashbox = await Cashboxes(tracked: true).Include(x => x.Currency).FirstOrDefaultAsync(x => x.Id == cashboxId, cancellationToken)
                      ?? throw Invalid(fieldName, "الصندوق غير موجود أو لا تملك صلاحية عليه.");

        if (!cashbox.IsActive)
        {
            throw Invalid(fieldName, $"الصندوق {cashbox.Code} غير مفعّل.");
        }

        if (expectedCurrencyId.HasValue && cashbox.CurrencyId != expectedCurrencyId.Value)
        {
            throw Invalid(fieldName, $"عملة الصندوق {cashbox.Code} ({cashbox.Currency.Code}) تختلف عن عملة العملية.");
        }

        return cashbox;
    }

    // ------------------------------------------------------------------ rules

    /// <summary>403 when a signed-in caller lacks an extra permission the operation needs.</summary>
    public void EnsurePermission(string permission, string message)
    {
        if (_currentUser.IsAuthenticated && !_currentUser.HasPermission(permission))
        {
            throw new ForbiddenException(message);
        }
    }

    /// <summary>422 when the caller approves his own request (four-eyes rule).</summary>
    public void EnsureOtherApprover(long? requestedByUserId, string what)
    {
        if (_options.AllowSelfApproval || !_currentUser.IsAuthenticated || requestedByUserId is null)
        {
            return;
        }

        if (requestedByUserId == _currentUser.UserId)
        {
            throw new BusinessRuleException($"لا يمكنك اعتماد {what} بنفسك: يجب أن يعتمدها مستخدم آخر غير مقدّم الطلب.");
        }
    }

    public static RequestValidationException Invalid(string fieldName, string message) =>
        new(new Dictionary<string, string[]> { [fieldName] = new[] { message } });
}
