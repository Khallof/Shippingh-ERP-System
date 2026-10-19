using Microsoft.EntityFrameworkCore;
using ShippingERP.Domain.Authorization;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Identity;

/// <summary>
/// Reads a user's active roles and the permissions those roles grant.
/// SystemAdmin always receives every permission in the catalog.
/// </summary>
public sealed class UserAccessReader
{
    private readonly AppDbContext _dbContext;

    public UserAccessReader(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserAccess> GetAsync(long userId, CancellationToken cancellationToken)
    {
        var roles = await (
                from userRole in _dbContext.UserRoles
                join role in _dbContext.Roles on userRole.RoleId equals role.Id
                where userRole.UserId == userId && role.IsActive
                select new { role.Id, role.Name })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var roleNames = roles
            .Where(role => role.Name != null)
            .Select(role => role.Name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (roleNames.Contains(Roles.SystemAdmin, StringComparer.Ordinal))
        {
            var allCodes = Permissions.All.Select(permission => permission.Code).ToList();
            return new UserAccess(roleNames, allCodes);
        }

        var roleIds = roles.Select(role => role.Id).ToList();

        var permissionCodes = await _dbContext.RolePermissions
            .AsNoTracking()
            .Where(rolePermission => roleIds.Contains(rolePermission.RoleId))
            .Select(rolePermission => rolePermission.Permission.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        permissionCodes.Sort(StringComparer.Ordinal);

        return new UserAccess(roleNames, permissionCodes);
    }
}

/// <summary>Active role names and permission codes of one user.</summary>
public sealed record UserAccess(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);
