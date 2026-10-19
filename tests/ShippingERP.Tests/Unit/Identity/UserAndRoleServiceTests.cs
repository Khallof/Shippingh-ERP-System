using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.DTOs.Auth;
using ShippingERP.Application.DTOs.Roles;
using ShippingERP.Application.DTOs.Users;
using ShippingERP.Application.Interfaces.Identity;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Enums;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Identity;

/// <summary>User administration and role permission changes, including their effect on sessions.</summary>
public sealed class UserAndRoleServiceTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();
    private long _adminId;

    public async Task InitializeAsync()
    {
        await _context.SeedRolesAndPermissionsAsync();
        _adminId = await _context.CreateUserAsync("admin", Roles.SystemAdmin);
        _context.CurrentUser.SignInAsSystemAdmin(_adminId);
    }

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private async Task<T> UseAsync<TService, T>(Func<TService, Task<T>> action)
        where TService : notnull
    {
        await using var scope = _context.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    [Fact]
    public async Task CreateUser_HashesPassword_AssignsRoles_AndAudits()
    {
        var created = await UseAsync<IUserService, UserDto>(service => service.CreateUserAsync(new CreateUserRequest
        {
            UserName = "acc1",
            Email = "acc1@test.local",
            Password = TestSettings.StrongPassword,
            FullName = "Sara Hassan",
            ArabicFullName = "سارة حسن",
            BranchId = null,
            EmployeeCode = "EMP-001",
            Roles = new List<string> { Roles.Accountant }
        }, CancellationToken.None));

        Assert.Equal(new[] { Roles.Accountant }, created.Roles);
        Assert.True(created.IsActive);
        Assert.Equal(_adminId, created.CreatedByUserId);

        var (passwordHash, audited) = await _context.QueryAsync(async db => (
            await db.Users.Where(u => u.Id == created.Id).Select(u => u.PasswordHash).SingleAsync(),
            await db.AuditLogs.AnyAsync(a => a.Action == AuditAction.UserCreated && a.UserId == created.Id && a.CreatedByUserId == _adminId)));

        Assert.False(string.IsNullOrEmpty(passwordHash));
        Assert.DoesNotContain(TestSettings.StrongPassword, passwordHash);
        Assert.True(audited);
    }

    [Fact]
    public async Task CreateUser_WithUnknownBranch_ReturnsValidationErrorOnBranchId()
    {
        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => UseAsync<IUserService, UserDto>(service =>
            service.CreateUserAsync(
                new CreateUserRequest
                {
                    UserName = "nobranch",
                    Email = "nobranch@test.local",
                    Password = TestSettings.StrongPassword,
                    FullName = "No Branch",
                    ArabicFullName = "بدون فرع",
                    BranchId = 999,
                    Roles = new List<string> { Roles.Driver }
                },
                CancellationToken.None)));

        Assert.True(exception.Errors.ContainsKey("branchId"));
    }

    [Fact]
    public async Task CreateUser_WithDuplicateUserName_ThrowsConflict()
    {
        await _context.CreateUserAsync("dup", Roles.Driver);

        await Assert.ThrowsAsync<ConflictException>(() => UseAsync<IUserService, UserDto>(service => service.CreateUserAsync(
            new CreateUserRequest
            {
                UserName = "dup",
                Email = "another@test.local",
                Password = TestSettings.StrongPassword,
                FullName = "Dup",
                ArabicFullName = "مكرر",
                Roles = new List<string> { Roles.Driver }
            },
            CancellationToken.None)));
    }

    [Fact]
    public async Task NonAdmin_CannotGrantSystemAdminRole()
    {
        _context.CurrentUser.SignOut();
        _context.CurrentUser.UserId = 999;
        _context.CurrentUser.Roles.Add(Roles.BranchManager);
        _context.CurrentUser.Permissions.Add(Permissions.Users.Manage);
        _context.CurrentUser.Permissions.Add(Permissions.Branches.ViewAll);

        await Assert.ThrowsAsync<ForbiddenException>(() => UseAsync<IUserService, UserDto>(service => service.CreateUserAsync(
            new CreateUserRequest
            {
                UserName = "sneaky",
                Email = "sneaky@test.local",
                Password = TestSettings.StrongPassword,
                FullName = "Sneaky",
                ArabicFullName = "متسلل",
                Roles = new List<string> { Roles.SystemAdmin }
            },
            CancellationToken.None)));
    }

    [Fact]
    public async Task DeactivateUser_RevokesRefreshTokens_ChangesStamp_AndBlocksLogin()
    {
        var userId = await _context.CreateUserAsync("leaver", Roles.ShippingAgent);
        var login = await UseAsync<IAuthService, AuthResponse>(service => service.LoginAsync(
            new LoginRequest { UserNameOrEmail = "leaver", Password = TestSettings.StrongPassword }, CancellationToken.None));
        var stampBefore = await _context.QueryAsync(db => db.Users.Where(u => u.Id == userId).Select(u => u.SecurityStamp).SingleAsync());

        var result = await UseAsync<IUserService, UserDto>(service => service.DeactivateUserAsync(userId, CancellationToken.None));

        Assert.False(result.IsActive);
        var (stampAfter, activeTokens) = await _context.QueryAsync(async db => (
            await db.Users.Where(u => u.Id == userId).Select(u => u.SecurityStamp).SingleAsync(),
            await db.RefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAtUtc == null)));

        Assert.NotEqual(stampBefore, stampAfter);
        Assert.Equal(0, activeTokens);

        // The refresh token was revoked by the deactivation: the session cannot be renewed.
        await Assert.ThrowsAsync<UnauthorizedException>(() => UseAsync<IAuthService, AuthResponse>(service =>
            service.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = login.RefreshToken }, CancellationToken.None)));

        // And signing in again is refused.
        await Assert.ThrowsAsync<ForbiddenException>(() => UseAsync<IAuthService, AuthResponse>(service =>
            service.LoginAsync(new LoginRequest { UserNameOrEmail = "leaver", Password = TestSettings.StrongPassword }, CancellationToken.None)));
    }

    [Fact]
    public async Task DeactivateYourself_IsRefused()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UseAsync<IUserService, UserDto>(service => service.DeactivateUserAsync(_adminId, CancellationToken.None)));
    }

    [Fact]
    public async Task LastActiveSystemAdmin_CannotBeDeactivated()
    {
        var otherAdminId = await _context.CreateUserAsync("admin2", Roles.SystemAdmin);
        _context.CurrentUser.SignInAsSystemAdmin(otherAdminId);

        // admin2 deactivates admin: allowed, admin2 is still active.
        await UseAsync<IUserService, UserDto>(service => service.DeactivateUserAsync(_adminId, CancellationToken.None));

        // A third admin cannot then deactivate admin2, the last active one.
        var thirdAdminId = await _context.CreateUserAsync("admin3", Roles.SystemAdmin, isActive: false);
        _context.CurrentUser.SignInAsSystemAdmin(thirdAdminId);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UseAsync<IUserService, UserDto>(service => service.DeactivateUserAsync(otherAdminId, CancellationToken.None)));
    }

    [Fact]
    public async Task UpdateRolePermissions_ReplacesList_ChangesStampOfRoleMembers_AndAudits()
    {
        var driverId = await _context.CreateUserAsync("driver9", Roles.Driver);
        var stampBefore = await _context.QueryAsync(db => db.Users.Where(u => u.Id == driverId).Select(u => u.SecurityStamp).SingleAsync());
        var driverRoleId = await _context.QueryAsync(db => db.Roles.Where(r => r.Name == Roles.Driver).Select(r => r.Id).SingleAsync());

        var role = await UseAsync<IRoleService, RoleDto>(service => service.UpdateRolePermissionsAsync(
            driverRoleId,
            new UpdateRolePermissionsRequest
            {
                Permissions = new List<string> { Permissions.Shipments.View, Permissions.Shipments.AssignDriver }
            },
            CancellationToken.None));

        Assert.Equal(new[] { Permissions.Shipments.AssignDriver, Permissions.Shipments.View }, role.Permissions);

        var stampAfter = await _context.QueryAsync(db => db.Users.Where(u => u.Id == driverId).Select(u => u.SecurityStamp).SingleAsync());
        Assert.NotEqual(stampBefore, stampAfter);

        // The next sign-in carries the new permission list.
        var login = await UseAsync<IAuthService, AuthResponse>(service => service.LoginAsync(
            new LoginRequest { UserNameOrEmail = "driver9", Password = TestSettings.StrongPassword }, CancellationToken.None));
        Assert.Equal(new[] { Permissions.Shipments.AssignDriver, Permissions.Shipments.View }, login.User.Permissions);

        Assert.True(await _context.QueryAsync(db =>
            db.AuditLogs.AnyAsync(a => a.Action == AuditAction.RolePermissionsChanged && a.EntityId == driverRoleId.ToString())));
    }

    [Fact]
    public async Task SystemAdminRolePermissions_CannotBeEdited()
    {
        var adminRoleId = await _context.QueryAsync(db => db.Roles.Where(r => r.Name == Roles.SystemAdmin).Select(r => r.Id).SingleAsync());

        await Assert.ThrowsAsync<BusinessRuleException>(() => UseAsync<IRoleService, RoleDto>(service =>
            service.UpdateRolePermissionsAsync(
                adminRoleId,
                new UpdateRolePermissionsRequest { Permissions = new List<string> { Permissions.Users.View } },
                CancellationToken.None)));
    }

    [Fact]
    public async Task Seeder_CreatesAllRolesAndPermissions_AndIsSafeToRunTwice()
    {
        await _context.SeedRolesAndPermissionsAsync();

        var (roleCount, permissionCount, adminPermissionCount) = await _context.QueryAsync(async db => (
            await db.Roles.CountAsync(),
            await db.Permissions.CountAsync(),
            await db.RolePermissions.CountAsync(rp => rp.Role.Name == Roles.SystemAdmin)));

        Assert.Equal(Roles.All.Count, roleCount);
        Assert.Equal(Permissions.All.Count, permissionCount);
        Assert.Equal(Permissions.All.Count, adminPermissionCount);
    }
}
