using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Users;
using ShippingERP.Application.Interfaces;
using ShippingERP.Application.Interfaces.Identity;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Entities.Identity;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Identity;

/// <summary>
/// User administration.
///
/// Branch rule: a caller WITHOUT the permission Branches.ViewAll only sees and manages users of their own branch.
/// Privilege rule: only a SystemAdmin can grant the SystemAdmin role or edit a SystemAdmin user.
/// Safety rule: the last active SystemAdmin can never be deactivated or lose the role, and nobody can deactivate themselves.
/// </summary>
public sealed class UserService : IUserService
{
    private const string UserEntityName = "User";

    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUserSessionCache _sessionCache;
    private readonly AuditLogWriter _auditLog;

    public UserService(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IUserSessionCache sessionCache,
        AuditLogWriter auditLog)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _sessionCache = sessionCache;
        _auditLog = auditLog;
    }

    private bool CanAccessAllBranches => _currentUserService.HasPermission(Permissions.Branches.ViewAll);

    public async Task<PagedResult<UserDto>> GetUsersAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        var users = ScopedUsers().AsNoTracking();

        if (CanAccessAllBranches && query.BranchId.HasValue)
        {
            var branchId = query.BranchId.Value;
            users = users.Where(user => user.BranchId == branchId);
        }

        if (query.IsActive.HasValue)
        {
            var isActive = query.IsActive.Value;
            users = users.Where(user => user.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var normalizedRole = _roleManager.NormalizeKey(query.Role.Trim());
            users = users.Where(user => _dbContext.UserRoles.Any(userRole =>
                userRole.UserId == user.Id &&
                _dbContext.Roles.Any(role => role.Id == userRole.RoleId && role.NormalizedName == normalizedRole)));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            users = users.Where(user =>
                user.UserName!.Contains(search) ||
                (user.Email != null && user.Email.Contains(search)) ||
                user.FullName.Contains(search) ||
                user.ArabicFullName.Contains(search) ||
                (user.EmployeeCode != null && user.EmployeeCode.Contains(search)) ||
                (user.PhoneNumber != null && user.PhoneNumber.Contains(search)));
        }

        var totalCount = await users.CountAsync(cancellationToken);

        var page = await ApplySorting(users, query)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var rolesByUser = await GetRoleNamesAsync(page.Select(user => user.Id).ToList(), cancellationToken);

        var items = page
            .Select(user => ToDto(user, rolesByUser.GetValueOrDefault(user.Id) ?? Array.Empty<string>()))
            .ToList();

        return new PagedResult<UserDto>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<UserDto> GetUserAsync(long id, CancellationToken cancellationToken)
    {
        var user = await ScopedUsers()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new NotFoundException("المستخدم", id);

        var rolesByUser = await GetRoleNamesAsync(new List<long> { user.Id }, cancellationToken);
        return ToDto(user, rolesByUser.GetValueOrDefault(user.Id) ?? Array.Empty<string>());
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManageBranch(request.BranchId);
        await EnsureBranchExistsAsync(request.BranchId, null, cancellationToken);

        var roleNames = await ResolveAssignableRolesAsync(request.Roles, cancellationToken);
        var employeeCode = NormalizeOptional(request.EmployeeCode);
        await EnsureEmployeeCodeIsFreeAsync(employeeCode, null, cancellationToken);

        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = true,
            PhoneNumber = NormalizeOptional(request.PhoneNumber),
            FullName = request.FullName.Trim(),
            ArabicFullName = request.ArabicFullName.Trim(),
            BranchId = request.BranchId,
            EmployeeCode = employeeCode,
            IsActive = true,
            LockoutEnabled = true
        };

        var userId = await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            // Identity validates the user name, e-mail uniqueness and password rules, then hashes the password.
            (await _userManager.CreateAsync(user, request.Password)).ThrowIfFailed();
            (await _userManager.AddToRolesAsync(user, roleNames)).ThrowIfFailed();

            _auditLog.Add(
                AuditAction.UserCreated,
                userId: user.Id,
                userName: user.UserName,
                entityName: UserEntityName,
                entityId: user.Id.ToString(),
                details: new { user.BranchId, user.EmployeeCode, roles = roleNames });

            await _dbContext.SaveChangesAsync(ct);
            return user.Id;
        }, cancellationToken);

        return await GetUserAsync(userId, cancellationToken);
    }

    public async Task<UserDto> UpdateUserAsync(long id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await FindScopedUserForUpdateAsync(id, cancellationToken);
        var currentRoles = (await _userManager.GetRolesAsync(user)).ToList();

        EnsureCanManageUser(currentRoles);
        EnsureCanManageBranch(request.BranchId);
        await EnsureBranchExistsAsync(request.BranchId, user.BranchId, cancellationToken);

        var newRoles = await ResolveAssignableRolesAsync(request.Roles, cancellationToken);
        var employeeCode = NormalizeOptional(request.EmployeeCode);
        await EnsureEmployeeCodeIsFreeAsync(employeeCode, user.Id, cancellationToken);

        var rolesToAdd = newRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();
        var rolesToRemove = currentRoles.Except(newRoles, StringComparer.OrdinalIgnoreCase).ToList();

        if (rolesToRemove.Contains(Roles.SystemAdmin, StringComparer.OrdinalIgnoreCase))
        {
            await EnsureAnotherActiveSystemAdminExistsAsync(user.Id, cancellationToken);
        }

        var email = request.Email.Trim();
        var emailChanged = !string.Equals(user.Email, email, StringComparison.Ordinal);
        var branchChanged = user.BranchId != request.BranchId;
        var rolesChanged = rolesToAdd.Count > 0 || rolesToRemove.Count > 0;

        var changes = new Dictionary<string, object?>();
        TrackChange(changes, "email", user.Email, email);
        TrackChange(changes, "phoneNumber", user.PhoneNumber, NormalizeOptional(request.PhoneNumber));
        TrackChange(changes, "fullName", user.FullName, request.FullName.Trim());
        TrackChange(changes, "arabicFullName", user.ArabicFullName, request.ArabicFullName.Trim());
        TrackChange(changes, "branchId", user.BranchId, request.BranchId);
        TrackChange(changes, "employeeCode", user.EmployeeCode, employeeCode);

        await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            user.PhoneNumber = NormalizeOptional(request.PhoneNumber);
            user.FullName = request.FullName.Trim();
            user.ArabicFullName = request.ArabicFullName.Trim();
            user.BranchId = request.BranchId;
            user.EmployeeCode = employeeCode;

            if (emailChanged)
            {
                // Also checks uniqueness and changes the security stamp.
                (await _userManager.SetEmailAsync(user, email)).ThrowIfFailed();
                user.EmailConfirmed = true;
            }

            (await _userManager.UpdateAsync(user)).ThrowIfFailed();

            if (rolesToRemove.Count > 0)
            {
                (await _userManager.RemoveFromRolesAsync(user, rolesToRemove)).ThrowIfFailed();
            }

            if (rolesToAdd.Count > 0)
            {
                (await _userManager.AddToRolesAsync(user, rolesToAdd)).ThrowIfFailed();
            }

            // Roles and branch are inside the access token: force the user's tokens to be refreshed.
            if (rolesChanged || branchChanged)
            {
                (await _userManager.UpdateSecurityStampAsync(user)).ThrowIfFailed();
            }

            _auditLog.Add(
                AuditAction.UserUpdated,
                userId: user.Id,
                userName: user.UserName,
                entityName: UserEntityName,
                entityId: user.Id.ToString(),
                details: new { changes, addedRoles = rolesToAdd, removedRoles = rolesToRemove });

            await _dbContext.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        if (rolesChanged || branchChanged || emailChanged)
        {
            _sessionCache.Invalidate(user.Id);
        }

        return await GetUserAsync(user.Id, cancellationToken);
    }

    public async Task<UserDto> ActivateUserAsync(long id, CancellationToken cancellationToken)
    {
        var user = await FindScopedUserForUpdateAsync(id, cancellationToken);
        EnsureCanManageUser((await _userManager.GetRolesAsync(user)).ToList());

        var wasActive = user.IsActive;
        var wasLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value.UtcDateTime > _dateTimeProvider.UtcNow;

        // Nothing to do: already active and not locked.
        if (wasActive && !wasLockedOut && user.AccessFailedCount == 0)
        {
            return await GetUserAsync(user.Id, cancellationToken);
        }

        await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            user.IsActive = true;

            // Also serves as "unlock": clears the lockout caused by wrong passwords.
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;

            (await _userManager.UpdateAsync(user)).ThrowIfFailed();

            _auditLog.Add(
                AuditAction.UserActivated,
                userId: user.Id,
                userName: user.UserName,
                entityName: UserEntityName,
                entityId: user.Id.ToString(),
                details: new { wasActive, wasLockedOut });

            await _dbContext.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        _sessionCache.Invalidate(user.Id);
        return await GetUserAsync(user.Id, cancellationToken);
    }

    public async Task<UserDto> DeactivateUserAsync(long id, CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId == id)
        {
            throw new BusinessRuleException("لا يمكنك إيقاف حسابك الخاص.");
        }

        var user = await FindScopedUserForUpdateAsync(id, cancellationToken);
        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        EnsureCanManageUser(roles);

        if (!user.IsActive)
        {
            return await GetUserAsync(user.Id, cancellationToken);
        }

        if (roles.Contains(Roles.SystemAdmin, StringComparer.OrdinalIgnoreCase))
        {
            await EnsureAnotherActiveSystemAdminExistsAsync(user.Id, cancellationToken);
        }

        var utcNow = _dateTimeProvider.UtcNow;

        await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            user.IsActive = false;

            // New security stamp = every access token of this user is rejected on the next request.
            (await _userManager.UpdateSecurityStampAsync(user)).ThrowIfFailed();

            // And no refresh token can create a new one.
            var activeTokens = await _dbContext.RefreshTokens
                .Where(token => token.UserId == user.Id && token.RevokedAtUtc == null)
                .ToListAsync(ct);

            foreach (var token in activeTokens)
            {
                token.Revoke(utcNow, _currentUserService.IpAddress, RefreshTokenRevokeReasons.UserDeactivated);
            }

            _auditLog.Add(
                AuditAction.UserDeactivated,
                userId: user.Id,
                userName: user.UserName,
                entityName: UserEntityName,
                entityId: user.Id.ToString(),
                details: new { revokedRefreshTokens = activeTokens.Count });

            await _dbContext.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        _sessionCache.Invalidate(user.Id);
        return await GetUserAsync(user.Id, cancellationToken);
    }

    /// <summary>Users visible to the caller (own branch only, unless the caller has Branches.ViewAll).</summary>
    private IQueryable<ApplicationUser> ScopedUsers()
    {
        IQueryable<ApplicationUser> users = _dbContext.Users;

        if (!CanAccessAllBranches)
        {
            var callerBranchId = _currentUserService.BranchId;
            users = users.Where(user => user.BranchId == callerBranchId);
        }

        return users;
    }

    private async Task<ApplicationUser> FindScopedUserForUpdateAsync(long id, CancellationToken cancellationToken)
    {
        return await ScopedUsers().FirstOrDefaultAsync(user => user.Id == id, cancellationToken)
               ?? throw new NotFoundException("المستخدم", id);
    }

    private void EnsureCanManageBranch(long? branchId)
    {
        if (!CanAccessAllBranches && branchId != _currentUserService.BranchId)
        {
            throw new ForbiddenException("لا يمكنك إدارة مستخدمين في فرع غير فرعك.");
        }
    }

    /// <summary>The branch must exist; a newly assigned branch must also be active.</summary>
    private async Task EnsureBranchExistsAsync(long? branchId, long? currentBranchId, CancellationToken cancellationToken)
    {
        if (branchId is null)
        {
            return;
        }

        var id = branchId.Value;
        var branch = await _dbContext.Branches
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

        if (branch is null || (!branch.IsActive && branchId != currentBranchId))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["branchId"] = new[] { branch is null ? "الفرع غير موجود." : "الفرع غير مفعّل." }
            });
        }
    }

    private void EnsureCanManageUser(IReadOnlyCollection<string> targetUserRoles)
    {
        if (!_currentUserService.IsSystemAdmin &&
            targetUserRoles.Contains(Roles.SystemAdmin, StringComparer.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("فقط مدير النظام يمكنه تعديل حساب مدير نظام.");
        }
    }

    /// <summary>
    /// Checks that every requested role exists and is active, and that the caller may grant it.
    /// Returns the role names exactly as stored.
    /// </summary>
    private async Task<List<string>> ResolveAssignableRolesAsync(
        IEnumerable<string> requestedRoles,
        CancellationToken cancellationToken)
    {
        var requested = requestedRoles
            .Select(role => role.Trim())
            .Where(role => role.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var normalizedNames = requested
            .Select(role => _roleManager.NormalizeKey(role) ?? role.ToUpperInvariant())
            .ToList();

        var roles = await _dbContext.Roles
            .AsNoTracking()
            .Where(role => role.NormalizedName != null && normalizedNames.Contains(role.NormalizedName))
            .ToListAsync(cancellationToken);

        var missing = requested
            .Where(name => roles.All(role => !string.Equals(role.Name, name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missing.Count > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["roles"] = new[] { $"أدوار غير موجودة: {string.Join(", ", missing)}" }
            });
        }

        var inactive = roles.Where(role => !role.IsActive).Select(role => role.Name).ToList();
        if (inactive.Count > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["roles"] = new[] { $"أدوار غير مفعلة: {string.Join(", ", inactive)}" }
            });
        }

        if (!_currentUserService.IsSystemAdmin && roles.Any(role => role.Name == Roles.SystemAdmin))
        {
            throw new ForbiddenException("فقط مدير النظام يمكنه منح دور مدير النظام.");
        }

        return roles.Select(role => role.Name!).ToList();
    }

    private async Task EnsureEmployeeCodeIsFreeAsync(string? employeeCode, long? exceptUserId, CancellationToken cancellationToken)
    {
        if (employeeCode is null)
        {
            return;
        }

        var taken = await _dbContext.Users.AnyAsync(
            user => user.EmployeeCode == employeeCode && (exceptUserId == null || user.Id != exceptUserId),
            cancellationToken);

        if (taken)
        {
            throw new ConflictException($"الرقم الوظيفي '{employeeCode}' مستخدم لموظف آخر.");
        }
    }

    private async Task EnsureAnotherActiveSystemAdminExistsAsync(long excludedUserId, CancellationToken cancellationToken)
    {
        var otherActiveAdmins = await (
                from userRole in _dbContext.UserRoles
                join role in _dbContext.Roles on userRole.RoleId equals role.Id
                join user in _dbContext.Users on userRole.UserId equals user.Id
                where role.Name == Roles.SystemAdmin && user.IsActive && user.Id != excludedUserId
                select user.Id)
            .CountAsync(cancellationToken);

        if (otherActiveAdmins == 0)
        {
            throw new BusinessRuleException("لا يمكن تنفيذ العملية: يجب أن يبقى مدير نظام نشط واحد على الأقل.");
        }
    }

    private async Task<Dictionary<long, IReadOnlyList<string>>> GetRoleNamesAsync(
        List<long> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<long, IReadOnlyList<string>>();
        }

        var rows = await (
                from userRole in _dbContext.UserRoles
                join role in _dbContext.Roles on userRole.RoleId equals role.Id
                where userIds.Contains(userRole.UserId)
                select new { userRole.UserId, role.Name })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(row => row.Name ?? string.Empty)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList());
    }

    private static IQueryable<ApplicationUser> ApplySorting(IQueryable<ApplicationUser> users, UserListQuery query)
    {
        var descending = query.IsDescending();

        var ordered = (query.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "fullname" => descending ? users.OrderByDescending(u => u.FullName) : users.OrderBy(u => u.FullName),
            "arabicfullname" => descending ? users.OrderByDescending(u => u.ArabicFullName) : users.OrderBy(u => u.ArabicFullName),
            "email" => descending ? users.OrderByDescending(u => u.Email) : users.OrderBy(u => u.Email),
            "employeecode" => descending ? users.OrderByDescending(u => u.EmployeeCode) : users.OrderBy(u => u.EmployeeCode),
            "createdatutc" => descending ? users.OrderByDescending(u => u.CreatedAtUtc) : users.OrderBy(u => u.CreatedAtUtc),
            "lastloginatutc" => descending ? users.OrderByDescending(u => u.LastLoginAtUtc) : users.OrderBy(u => u.LastLoginAtUtc),
            _ => descending ? users.OrderByDescending(u => u.UserName) : users.OrderBy(u => u.UserName)
        };

        // Id as a tie-breaker keeps paging stable.
        return ordered.ThenBy(u => u.Id);
    }

    private UserDto ToDto(ApplicationUser user, IReadOnlyList<string> roles)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        var lockoutEndUtc = user.LockoutEnd?.UtcDateTime;

        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            FullName = user.FullName,
            ArabicFullName = user.ArabicFullName,
            BranchId = user.BranchId,
            EmployeeCode = user.EmployeeCode,
            IsActive = user.IsActive,
            IsLockedOut = lockoutEndUtc.HasValue && lockoutEndUtc.Value > utcNow,
            LockoutEndUtc = lockoutEndUtc.HasValue && lockoutEndUtc.Value > utcNow ? lockoutEndUtc : null,
            LastLoginAtUtc = user.LastLoginAtUtc,
            Roles = roles,
            CreatedAtUtc = user.CreatedAtUtc,
            CreatedByUserId = user.CreatedByUserId,
            UpdatedAtUtc = user.UpdatedAtUtc,
            UpdatedByUserId = user.UpdatedByUserId
        };
    }

    private static void TrackChange<T>(Dictionary<string, object?> changes, string field, T oldValue, T newValue)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            changes[field] = new { from = oldValue, to = newValue };
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
