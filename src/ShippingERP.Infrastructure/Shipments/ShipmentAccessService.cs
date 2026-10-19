using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Entities.Shipments;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Shipments;

/// <summary>
/// Decides which shipments the caller can see and act on.
///
/// 1. Not signed in (seeding, background jobs) or Branches.ViewAll: everything.
/// 2. Driver role WITHOUT Shipments.AssignDriver ("driver-only" user): only shipments assigned to the caller's
///    driver profile (DriverProfile.UserId = caller). A user in the Driver role with no active driver profile
///    sees nothing.
/// 3. Everybody else: shipments whose origin, current, destination or return branch is the caller's branch.
///
/// Records outside the scope are reported as 404 by the services, never as 403, so their existence is not leaked.
/// </summary>
public sealed class ShipmentAccessService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IBranchAccessService _branchAccess;

    private bool _driverLoaded;
    private long? _driverId;

    public ShipmentAccessService(AppDbContext dbContext, ICurrentUserService currentUser, IBranchAccessService branchAccess)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _branchAccess = branchAccess;
    }

    public IBranchAccessService BranchAccess => _branchAccess;

    /// <summary>True for a driver who may only see his own assigned work.</summary>
    public bool IsDriverOnly =>
        _currentUser.IsAuthenticated &&
        _currentUser.IsInRole(Roles.Driver) &&
        !_currentUser.IsSystemAdmin &&
        !_currentUser.HasPermission(Permissions.Shipments.AssignDriver);

    /// <summary>The caller's active driver profile id (null when the caller is not a driver).</summary>
    public async Task<long?> GetCurrentDriverIdAsync(CancellationToken cancellationToken)
    {
        if (_driverLoaded)
        {
            return _driverId;
        }

        var userId = _currentUser.UserId;
        _driverId = userId is null
            ? null
            : await _dbContext.DriverProfiles
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.IsActive)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

        _driverLoaded = true;
        return _driverId;
    }

    /// <summary>Applies the visibility rules to a shipment query.</summary>
    public async Task<IQueryable<Shipment>> ScopeAsync(IQueryable<Shipment> query, CancellationToken cancellationToken)
    {
        if (IsDriverOnly)
        {
            var driverId = await GetCurrentDriverIdAsync(cancellationToken);
            return driverId is null
                ? query.Where(_ => false)
                : query.Where(x => x.AssignedDriverId == driverId);
        }

        if (_branchAccess.CanAccessAllBranches)
        {
            return query;
        }

        var branchId = _branchAccess.CurrentBranchId;
        return query.Where(x =>
            x.OriginBranchId == branchId ||
            x.CurrentBranchId == branchId ||
            x.DestinationBranchId == branchId ||
            x.ReturnBranchId == branchId);
    }

    /// <summary>Visible shipments (tracked).</summary>
    public Task<IQueryable<Shipment>> ShipmentsAsync(CancellationToken cancellationToken) =>
        ScopeAsync(_dbContext.Shipments, cancellationToken);

    /// <summary>Loads a visible shipment for update, or throws 404.</summary>
    public async Task<Shipment> FindForUpdateAsync(long id, CancellationToken cancellationToken, bool includePackages = false)
    {
        IQueryable<Shipment> query = await ShipmentsAsync(cancellationToken);
        if (includePackages)
        {
            query = query.Include(x => x.Packages);
        }

        return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
               ?? throw new NotFoundException("الشحنة", id);
    }

    /// <summary>Throws 404 when the shipment does not exist or is outside the caller's scope.</summary>
    public async Task EnsureVisibleAsync(long id, CancellationToken cancellationToken)
    {
        var query = await ScopeAsync(_dbContext.Shipments.AsNoTracking(), cancellationToken);
        if (!await query.AnyAsync(x => x.Id == id, cancellationToken))
        {
            throw new NotFoundException("الشحنة", id);
        }
    }

    /// <summary>
    /// Operations that physically handle the shipment (scan, cancel, assign, correct) must be done by the branch
    /// that holds it. A driver-only user is limited by the visibility rules instead.
    /// </summary>
    public void EnsureCanOperate(Shipment shipment)
    {
        if (IsDriverOnly)
        {
            return;
        }

        _branchAccess.EnsureCanAccess(shipment.CurrentBranchId);
    }
}
