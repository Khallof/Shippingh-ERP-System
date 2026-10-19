using ShippingERP.Application.Interfaces;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.Tests.Common;

/// <summary>A caller the test controls (anonymous by default).</summary>
public sealed class TestCurrentUser : ICurrentUserService
{
    public long? UserId { get; set; }

    public bool IsAuthenticated => UserId.HasValue;

    public long? BranchId { get; set; }

    public HashSet<string> Roles { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Permissions { get; } = new(StringComparer.Ordinal);

    public bool IsSystemAdmin => Roles.Contains(ShippingERP.Domain.Authorization.Roles.SystemAdmin);

    public string? IpAddress { get; set; } = "10.0.0.1";

    public string? UserAgent { get; set; } = "xunit";

    public bool HasPermission(string permission) =>
        IsAuthenticated && (IsSystemAdmin || Permissions.Contains(permission));

    public bool IsInRole(string role) => IsAuthenticated && Roles.Contains(role);

    /// <summary>Signs the test in as a SystemAdmin with the given id.</summary>
    public void SignInAsSystemAdmin(long userId)
    {
        UserId = userId;
        BranchId = null;
        Roles.Clear();
        Roles.Add(ShippingERP.Domain.Authorization.Roles.SystemAdmin);
    }

    /// <summary>Signs the test in as a normal user of a branch with exactly these permissions.</summary>
    public void SignInAs(long userId, long? branchId, params string[] permissions)
    {
        UserId = userId;
        BranchId = branchId;
        Roles.Clear();
        Permissions.Clear();
        foreach (var permission in permissions)
        {
            Permissions.Add(permission);
        }
    }

    /// <summary>Signs the test in with one role (e.g. Driver) and exactly these permissions.</summary>
    public void SignInWithRole(long userId, long? branchId, string role, params string[] permissions)
    {
        SignInAs(userId, branchId, permissions);
        Roles.Add(role);
    }

    public void SignOut()
    {
        UserId = null;
        BranchId = null;
        Roles.Clear();
        Permissions.Clear();
    }
}
