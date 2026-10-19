using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.Infrastructure.MasterData;

/// <inheritdoc cref="IBranchAccessService"/>
public sealed class BranchAccessService : IBranchAccessService
{
    private readonly ICurrentUserService _currentUserService;

    public BranchAccessService(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// True for SystemAdmin, users with Branches.ViewAll, and code that runs without a signed-in user
    /// (seeding, background jobs).
    /// </summary>
    public bool CanAccessAllBranches =>
        !_currentUserService.IsAuthenticated || _currentUserService.HasPermission(Permissions.Branches.ViewAll);

    public long? CurrentBranchId => _currentUserService.BranchId;

    public bool CanAccess(long? branchId) => CanAccessAllBranches || branchId == CurrentBranchId;

    public void EnsureCanAccess(long? branchId)
    {
        if (!CanAccess(branchId))
        {
            throw new ForbiddenException("لا يمكنك الوصول إلى بيانات فرع غير فرعك.");
        }
    }
}
