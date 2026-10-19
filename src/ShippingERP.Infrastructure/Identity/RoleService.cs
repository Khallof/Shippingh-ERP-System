using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Roles;
using ShippingERP.Application.Interfaces.Identity;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Entities.Identity;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Persistence;
using PermissionCatalog = ShippingERP.Domain.Authorization.Permissions;

namespace ShippingERP.Infrastructure.Identity;

/// <summary>
/// Roles and their permissions.
///
/// When a role's permissions change, every user in that role gets a new security stamp, so their current
/// access tokens stop working at once and the next refresh issues a token with the new permission list.
/// SystemAdmin's permissions cannot be edited: it always holds every permission.
/// </summary>
public sealed class RoleService : IRoleService
{
    private const string RoleEntityName = "Role";

    private readonly AppDbContext _dbContext;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IUserSessionCache _sessionCache;
    private readonly AuditLogWriter _auditLog;

    public RoleService(
        AppDbContext dbContext,
        RoleManager<ApplicationRole> roleManager,
        IUserSessionCache sessionCache,
        AuditLogWriter auditLog)
    {
        _dbContext = dbContext;
        _roleManager = roleManager;
        _sessionCache = sessionCache;
        _auditLog = auditLog;
    }

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken)
    {
        var rows = await QueryRoles()
            .OrderByDescending(role => role.IsSystemRole)
            .ThenBy(role => role.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public async Task<RoleDto> GetRoleAsync(long id, CancellationToken cancellationToken)
    {
        var row = await QueryRoles().FirstOrDefaultAsync(role => role.Id == id, cancellationToken)
                  ?? throw new NotFoundException("الدور", id);

        return ToDto(row);
    }

    public IReadOnlyList<PermissionDto> GetPermissionCatalog()
    {
        return PermissionCatalog.All
            .Select(permission => new PermissionDto
            {
                Code = permission.Code,
                Module = permission.Module,
                Description = permission.Description
            })
            .ToList();
    }

    public async Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (await _roleManager.RoleExistsAsync(name))
        {
            throw new ConflictException($"الدور '{name}' موجود من قبل.");
        }

        var permissions = await LoadPermissionsAsync(request.Permissions, cancellationToken);

        var roleId = await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            var role = new ApplicationRole(name)
            {
                ArabicName = request.ArabicName.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                IsSystemRole = false,
                IsActive = true
            };

            (await _roleManager.CreateAsync(role)).ThrowIfFailed();

            foreach (var permission in permissions)
            {
                _dbContext.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            }

            _auditLog.Add(
                AuditAction.RoleCreated,
                entityName: RoleEntityName,
                entityId: role.Id.ToString(),
                details: new { role = role.Name, permissions = permissions.Select(p => p.Code).ToList() });

            await _dbContext.SaveChangesAsync(ct);
            return role.Id;
        }, cancellationToken);

        return await GetRoleAsync(roleId, cancellationToken);
    }

    public async Task<RoleDto> UpdateRolePermissionsAsync(
        long id,
        UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles
            .Include(candidate => candidate.RolePermissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new NotFoundException("الدور", id);

        if (role.Name == Roles.SystemAdmin)
        {
            throw new BusinessRuleException("لا يمكن تعديل صلاحيات دور مدير النظام؛ فهو يملك جميع الصلاحيات دائماً.");
        }

        var requestedPermissions = await LoadPermissionsAsync(request.Permissions, cancellationToken);
        var requestedCodes = requestedPermissions.Select(permission => permission.Code).ToHashSet(StringComparer.Ordinal);
        var currentCodes = role.RolePermissions.Select(rp => rp.Permission.Code).ToHashSet(StringComparer.Ordinal);

        var toRemove = role.RolePermissions.Where(rp => !requestedCodes.Contains(rp.Permission.Code)).ToList();
        var toAdd = requestedPermissions.Where(permission => !currentCodes.Contains(permission.Code)).ToList();

        if (toRemove.Count == 0 && toAdd.Count == 0)
        {
            return await GetRoleAsync(id, cancellationToken);
        }

        // Users whose tokens must be refreshed.
        var affectedUsers = await _dbContext.Users
            .Where(user => _dbContext.UserRoles.Any(userRole => userRole.UserId == user.Id && userRole.RoleId == id))
            .ToListAsync(cancellationToken);

        _dbContext.RolePermissions.RemoveRange(toRemove);

        foreach (var permission in toAdd)
        {
            _dbContext.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }

        foreach (var user in affectedUsers)
        {
            user.SecurityStamp = NewSecurityStamp();
        }

        // Marks the role as modified so UpdatedAtUtc/UpdatedByUserId are filled by the interceptor.
        role.ConcurrencyStamp = Guid.NewGuid().ToString();

        _auditLog.Add(
            AuditAction.RolePermissionsChanged,
            entityName: RoleEntityName,
            entityId: role.Id.ToString(),
            details: new
            {
                role = role.Name,
                added = toAdd.Select(permission => permission.Code).ToList(),
                removed = toRemove.Select(rp => rp.Permission.Code).ToList(),
                affectedUsers = affectedUsers.Count
            });

        // Everything above is saved by ONE SaveChanges call, which EF Core runs in one database transaction.
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var user in affectedUsers)
        {
            _sessionCache.Invalidate(user.Id);
        }

        return await GetRoleAsync(id, cancellationToken);
    }

    /// <summary>Loads the Permission rows for the codes (validated by the request validator).</summary>
    private async Task<List<Permission>> LoadPermissionsAsync(IEnumerable<string> codes, CancellationToken cancellationToken)
    {
        var distinctCodes = codes.Distinct(StringComparer.Ordinal).ToList();

        var permissions = await _dbContext.Permissions
            .Where(permission => distinctCodes.Contains(permission.Code))
            .ToListAsync(cancellationToken);

        var missing = distinctCodes
            .Where(code => permissions.All(permission => permission.Code != code))
            .ToList();

        if (missing.Count > 0)
        {
            // Happens only if the code exists in Permissions.cs but the seeder has not run yet.
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["permissions"] = new[] { $"صلاحيات غير موجودة في قاعدة البيانات: {string.Join(", ", missing)}" }
            });
        }

        return permissions;
    }

    private IQueryable<RoleRow> QueryRoles()
    {
        return _dbContext.Roles
            .AsNoTracking()
            .Select(role => new RoleRow
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                ArabicName = role.ArabicName,
                Description = role.Description,
                IsSystemRole = role.IsSystemRole,
                IsActive = role.IsActive,
                CreatedAtUtc = role.CreatedAtUtc,
                UpdatedAtUtc = role.UpdatedAtUtc,
                Permissions = role.RolePermissions.Select(rp => rp.Permission.Code).ToList(),
                UserCount = _dbContext.UserRoles.Count(userRole =>
                    userRole.RoleId == role.Id &&
                    _dbContext.Users.Any(user => user.Id == userRole.UserId && user.IsActive))
            });
    }

    private static RoleDto ToDto(RoleRow row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        ArabicName = row.ArabicName,
        Description = row.Description,
        IsSystemRole = row.IsSystemRole,
        IsActive = row.IsActive,
        UserCount = row.UserCount,
        Permissions = row.Permissions.OrderBy(code => code, StringComparer.Ordinal).ToList(),
        CreatedAtUtc = row.CreatedAtUtc,
        UpdatedAtUtc = row.UpdatedAtUtc
    };

    /// <summary>A new random stamp (20 random bytes as hex). Any change of the value invalidates old tokens.</summary>
    private static string NewSecurityStamp() =>
        Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(20));

    private sealed class RoleRow
    {
        public long Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string ArabicName { get; init; } = string.Empty;
        public string? Description { get; init; }
        public bool IsSystemRole { get; init; }
        public bool IsActive { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? UpdatedAtUtc { get; init; }
        public List<string> Permissions { get; init; } = new();
        public int UserCount { get; init; }
    }
}
