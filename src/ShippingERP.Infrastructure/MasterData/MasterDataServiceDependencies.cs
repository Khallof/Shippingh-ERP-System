using ShippingERP.Application.Interfaces;
using ShippingERP.Infrastructure.Identity;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.MasterData;

/// <summary>
/// The services every master-data service needs, bundled so constructors stay short.
/// Registered as scoped (one per HTTP request), like the DbContext.
/// </summary>
public sealed class MasterDataServiceDependencies
{
    public MasterDataServiceDependencies(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        IBranchAccessService branchAccess,
        IDateTimeProvider dateTimeProvider,
        AuditLogWriter auditLog)
    {
        DbContext = dbContext;
        CurrentUser = currentUser;
        BranchAccess = branchAccess;
        DateTimeProvider = dateTimeProvider;
        AuditLog = auditLog;
    }

    public AppDbContext DbContext { get; }

    public ICurrentUserService CurrentUser { get; }

    public IBranchAccessService BranchAccess { get; }

    public IDateTimeProvider DateTimeProvider { get; }

    public AuditLogWriter AuditLog { get; }
}
