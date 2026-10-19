using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Entities.Identity;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Identity;

namespace ShippingERP.Infrastructure.Persistence.Seeding;

/// <summary>
/// Runs at every API start (safe to repeat):
/// 1. Inserts missing permissions from Domain/Authorization/Permissions.cs (and refreshes their descriptions).
/// 2. Creates missing built-in roles with their default permissions; keeps SystemAdmin holding ALL permissions.
/// 3. Creates the first SystemAdmin user ONLY if no SystemAdmin exists yet, using the credentials from
///    the environment variables Seed__AdminUserName, Seed__AdminEmail and Seed__AdminPassword.
///    Nothing about the administrator is hardcoded in the source code.
/// </summary>
public sealed class IdentitySeeder
{
    private readonly AppDbContext _dbContext;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditLogWriter _auditLog;
    private readonly SeedOptions _options;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        AppDbContext dbContext,
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        AuditLogWriter auditLog,
        IOptions<SeedOptions> options,
        ILogger<IdentitySeeder> logger)
    {
        _dbContext = dbContext;
        _roleManager = roleManager;
        _userManager = userManager;
        _auditLog = auditLog;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Seeding is disabled (Seed:Enabled = false)");
            return;
        }

        if (_dbContext.Database.IsRelational())
        {
            var pendingMigrations = (await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pendingMigrations.Count > 0)
            {
                _logger.LogWarning(
                    "Seeding skipped: {Count} migration(s) are not applied yet ({Migrations}). " +
                    "Run 'dotnet ef database update' (or set Database__ApplyMigrationsOnStartup=true) and restart",
                    pendingMigrations.Count,
                    string.Join(", ", pendingMigrations));
                return;
            }
        }

        var newPermissionCodes = await SeedPermissionsAsync(cancellationToken);
        await SeedRolesAsync(newPermissionCodes, cancellationToken);
        await SeedAdministratorAsync(cancellationToken);
    }

    /// <summary>Returns the codes inserted by this run (new permissions added to Permissions.cs).</summary>
    private async Task<HashSet<string>> SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Permissions.ToDictionaryAsync(permission => permission.Code, cancellationToken);
        var added = new HashSet<string>(StringComparer.Ordinal);

        foreach (var definition in Permissions.All)
        {
            if (existing.TryGetValue(definition.Code, out var permission))
            {
                // Keep module and description in sync with the code.
                permission.Module = definition.Module;
                permission.Description = definition.Description;
                continue;
            }

            _dbContext.Permissions.Add(new Permission
            {
                Code = definition.Code,
                Module = definition.Module,
                Description = definition.Description
            });
            added.Add(definition.Code);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (added.Count > 0)
        {
            _logger.LogInformation("Seeded {Count} new permission(s)", added.Count);
        }

        return added;
    }

    private async Task SeedRolesAsync(HashSet<string> newPermissionCodes, CancellationToken cancellationToken)
    {
        var permissionIdsByCode = await _dbContext.Permissions
            .ToDictionaryAsync(permission => permission.Code, permission => permission.Id, cancellationToken);

        foreach (var definition in Roles.All)
        {
            var role = await _roleManager.FindByNameAsync(definition.Name);

            if (role is null)
            {
                role = new ApplicationRole(definition.Name)
                {
                    ArabicName = definition.ArabicName,
                    Description = definition.Description,
                    IsSystemRole = true,
                    IsActive = true
                };

                (await _roleManager.CreateAsync(role)).ThrowIfFailed();

                foreach (var code in definition.DefaultPermissions)
                {
                    _dbContext.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permissionIdsByCode[code]
                    });
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Seeded role {Role} with {Count} permission(s)", definition.Name, definition.DefaultPermissions.Count);
                continue;
            }

            // Existing role: never overwrite an administrator's permission choices,
            // except SystemAdmin, which must always hold every permission (including newly added ones).
            if (definition.Name != Roles.SystemAdmin)
            {
                // A permission that did not exist before this run is granted to the built-in roles that
                // list it as a default (e.g. Pricing.View for ShippingAgent, added in step 3).
                var defaultsToGrant = definition.DefaultPermissions
                    .Where(code => newPermissionCodes.Contains(code))
                    .Select(code => permissionIdsByCode[code])
                    .ToList();

                if (defaultsToGrant.Count > 0)
                {
                    var alreadyAssigned = await _dbContext.RolePermissions
                        .Where(rolePermission => rolePermission.RoleId == role.Id)
                        .Select(rolePermission => rolePermission.PermissionId)
                        .ToListAsync(cancellationToken);

                    foreach (var permissionId in defaultsToGrant.Except(alreadyAssigned))
                    {
                        _dbContext.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
                    }

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation(
                        "Granted {Count} new default permission(s) to role {Role}",
                        defaultsToGrant.Count,
                        definition.Name);
                }

                continue;
            }

            if (definition.Name == Roles.SystemAdmin)
            {
                var assigned = await _dbContext.RolePermissions
                    .Where(rolePermission => rolePermission.RoleId == role.Id)
                    .Select(rolePermission => rolePermission.PermissionId)
                    .ToListAsync(cancellationToken);

                var missing = permissionIdsByCode.Values.Except(assigned).ToList();
                foreach (var permissionId in missing)
                {
                    _dbContext.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
                }

                if (missing.Count > 0)
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Granted {Count} new permission(s) to SystemAdmin", missing.Count);
                }
            }
        }
    }

    private async Task SeedAdministratorAsync(CancellationToken cancellationToken)
    {
        var existingAdmins = await _userManager.GetUsersInRoleAsync(Roles.SystemAdmin);
        if (existingAdmins.Count > 0)
        {
            return;
        }

        var userName = _options.AdminUserName?.Trim();
        var email = _options.AdminEmail?.Trim();
        var password = _options.AdminPassword;

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning(
                "No SystemAdmin user exists and the initial administrator is not configured. " +
                "Set the environment variables Seed__AdminUserName, Seed__AdminEmail and Seed__AdminPassword, " +
                "then restart the API");
            return;
        }

        if (await _userManager.FindByNameAsync(userName) is not null || await _userManager.FindByEmailAsync(email) is not null)
        {
            // Never silently promote an existing account to administrator.
            throw new InvalidOperationException(
                $"Cannot create the initial administrator: a user with the name '{userName}' or e-mail '{email}' already exists " +
                "but is not a SystemAdmin. Choose other values for Seed__AdminUserName / Seed__AdminEmail.");
        }

        var administrator = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            FullName = _options.AdminFullName,
            ArabicFullName = _options.AdminArabicFullName,
            IsActive = true,
            LockoutEnabled = true
        };

        await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            var createResult = await _userManager.CreateAsync(administrator, password);
            if (!createResult.Succeeded)
            {
                // Descriptions only, never the password itself.
                throw new InvalidOperationException(
                    "Cannot create the initial administrator: " +
                    string.Join(" ", createResult.Errors.Select(error => $"[{error.Code}] {error.Description}")) +
                    " Check Seed__AdminUserName / Seed__AdminEmail / Seed__AdminPassword " +
                    "(password: 8+ characters with upper case, lower case, digit and symbol).");
            }

            (await _userManager.AddToRoleAsync(administrator, Roles.SystemAdmin)).ThrowIfFailed();

            _auditLog.Add(
                AuditAction.UserCreated,
                userId: administrator.Id,
                userName: administrator.UserName,
                entityName: "User",
                entityId: administrator.Id.ToString(),
                details: new { source = "Seed", roles = new[] { Roles.SystemAdmin } });

            await _dbContext.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        _logger.LogWarning(
            "Initial administrator '{UserName}' (id {UserId}) was created. " +
            "Sign in, change the password, and remove Seed__AdminPassword from the environment",
            administrator.UserName,
            administrator.Id);
    }
}
