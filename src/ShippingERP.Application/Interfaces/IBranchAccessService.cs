namespace ShippingERP.Application.Interfaces;

/// <summary>
/// Branch-level data access. A user WITHOUT the permission Branches.ViewAll only reads and writes data of
/// their own branch (the branch_id claim of the token). SystemAdmin and users with Branches.ViewAll see all.
/// </summary>
public interface IBranchAccessService
{
    bool CanAccessAllBranches { get; }

    /// <summary>The caller's branch (null = head office / no branch).</summary>
    long? CurrentBranchId { get; }

    bool CanAccess(long? branchId);

    /// <summary>Throws ForbiddenException (403) when the caller may not use this branch.</summary>
    void EnsureCanAccess(long? branchId);
}
