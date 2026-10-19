using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Auth;
using ShippingERP.Application.Interfaces.Identity;
using ShippingERP.Domain.Authorization;
using ShippingERP.Domain.Entities.Identity;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Identity;
using ShippingERP.Tests.Common;

namespace ShippingERP.Tests.Unit.Identity;

/// <summary>
/// Sign-in, wrong password, inactive user, lockout, refresh-token rotation and reuse detection,
/// sign-out and password change. Uses the real services on an in-memory database.
/// </summary>
public sealed class AuthServiceTests : IAsyncLifetime
{
    private readonly IdentityTestContext _context = new();

    public Task InitializeAsync() => _context.SeedRolesAndPermissionsAsync();

    public Task DisposeAsync() => _context.DisposeAsync().AsTask();

    private async Task<AuthResponse> LoginAsync(string login, string password = TestSettings.StrongPassword)
    {
        await using var scope = _context.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        return await authService.LoginAsync(
            new LoginRequest { UserNameOrEmail = login, Password = password },
            CancellationToken.None);
    }

    private async Task<AuthResponse> RefreshAsync(string refreshToken)
    {
        await using var scope = _context.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        return await authService.RefreshTokenAsync(
            new RefreshTokenRequest { RefreshToken = refreshToken },
            CancellationToken.None);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsTokens_RolesAndPermissions()
    {
        var userId = await _context.CreateUserAsync("agent1", Roles.ShippingAgent, branchId: 5);

        var response = await LoginAsync("agent1");

        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal(userId, response.User.Id);
        Assert.Equal((long?)5, response.User.BranchId);
        Assert.Equal(new[] { Roles.ShippingAgent }, response.User.Roles);
        Assert.Contains(Permissions.Shipments.Create, response.User.Permissions);
        Assert.DoesNotContain(Permissions.Accounting.ApproveJournal, response.User.Permissions);

        var (user, storedTokens, audit) = await _context.QueryAsync(async db => (
            await db.Users.SingleAsync(u => u.Id == userId),
            await db.RefreshTokens.Where(t => t.UserId == userId).ToListAsync(),
            await db.AuditLogs.Where(a => a.UserId == userId).ToListAsync()));

        Assert.NotNull(user.LastLoginAtUtc);

        // Only the hash is stored, never the token itself.
        var stored = Assert.Single(storedTokens);
        Assert.NotEqual(response.RefreshToken, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
        Assert.Equal("10.0.0.1", stored.CreatedByIp);
        Assert.Equal("xunit", stored.UserAgent);

        Assert.Contains(audit, a => a.Action == AuditAction.Login && a.Succeeded && a.CreatedByUserId == userId);
    }

    [Fact]
    public async Task Login_WithEmail_Works()
    {
        await _context.CreateUserAsync("driver1", Roles.Driver);

        var response = await LoginAsync("driver1@test.local");

        Assert.Equal("driver1", response.User.UserName);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ThrowsInvalidCredentials_CountsFailure_AndAudits()
    {
        var userId = await _context.CreateUserAsync("agent2", Roles.ShippingAgent);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => LoginAsync("agent2", "Wrong#Pass2026"));

        Assert.Equal(ErrorCodes.InvalidCredentials, exception.ErrorCode);

        var (user, audit) = await _context.QueryAsync(async db => (
            await db.Users.SingleAsync(u => u.Id == userId),
            await db.AuditLogs.Where(a => a.UserId == userId).ToListAsync()));

        Assert.Equal(1, user.AccessFailedCount);
        Assert.Contains(audit, a => a.Action == AuditAction.LoginFailed && !a.Succeeded);
        Assert.Equal(0, await _context.QueryAsync(db => db.RefreshTokens.CountAsync(t => t.UserId == userId)));
    }

    [Fact]
    public async Task Login_WithUnknownUser_ThrowsSameInvalidCredentialsError_AndAudits()
    {
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => LoginAsync("nobody"));

        Assert.Equal(ErrorCodes.InvalidCredentials, exception.ErrorCode);
        Assert.True(await _context.QueryAsync(db =>
            db.AuditLogs.AnyAsync(a => a.Action == AuditAction.LoginFailed && a.UserName == "nobody" && a.UserId == null)));
    }

    [Fact]
    public async Task Login_FiveWrongPasswords_LocksTheAccount_EvenForTheCorrectPassword()
    {
        await _context.CreateUserAsync("agent3", Roles.ShippingAgent);

        for (var attempt = 1; attempt < IdentityServiceRegistration.MaxFailedAccessAttempts; attempt++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() => LoginAsync("agent3", "Wrong#Pass2026"));
        }

        var lockedOnFifth = await Assert.ThrowsAsync<ForbiddenException>(() => LoginAsync("agent3", "Wrong#Pass2026"));
        Assert.Equal(ErrorCodes.AccountLocked, lockedOnFifth.ErrorCode);

        var lockedWithCorrectPassword = await Assert.ThrowsAsync<ForbiddenException>(() => LoginAsync("agent3"));
        Assert.Equal(ErrorCodes.AccountLocked, lockedWithCorrectPassword.ErrorCode);
    }

    [Fact]
    public async Task Login_InactiveUser_ThrowsAccountDisabled_AndIssuesNoToken()
    {
        var userId = await _context.CreateUserAsync("former", Roles.Accountant, isActive: false);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => LoginAsync("former"));

        Assert.Equal(ErrorCodes.AccountDisabled, exception.ErrorCode);
        Assert.Equal(0, await _context.QueryAsync(db => db.RefreshTokens.CountAsync(t => t.UserId == userId)));
    }

    [Fact]
    public async Task RefreshToken_RotatesToken_OldOneIsRevokedAndLinkedToTheNewOne()
    {
        var userId = await _context.CreateUserAsync("agent4", Roles.ShippingAgent);
        var login = await LoginAsync("agent4");

        var refreshed = await RefreshAsync(login.RefreshToken);

        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));

        var tokens = await _context.QueryAsync(db => db.RefreshTokens.Where(t => t.UserId == userId).ToListAsync());
        Assert.Equal(2, tokens.Count);

        var oldToken = tokens.Single(t => t.RevokedAtUtc != null);
        var newToken = tokens.Single(t => t.RevokedAtUtc == null);

        Assert.Equal(RefreshTokenRevokeReasons.Rotated, oldToken.RevokedReason);
        Assert.Equal(newToken.Id, oldToken.ReplacedByTokenId);
        Assert.Equal(oldToken.FamilyId, newToken.FamilyId);

        // The new token works too.
        var refreshedAgain = await RefreshAsync(refreshed.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(refreshedAgain.AccessToken));
    }

    [Fact]
    public async Task RefreshToken_ReusingARotatedToken_RevokesTheWholeSession()
    {
        var userId = await _context.CreateUserAsync("agent5", Roles.ShippingAgent);
        var login = await LoginAsync("agent5");
        var refreshed = await RefreshAsync(login.RefreshToken);

        // The first token is presented again (e.g. stolen copy).
        var reuse = await Assert.ThrowsAsync<UnauthorizedException>(() => RefreshAsync(login.RefreshToken));
        Assert.Equal(ErrorCodes.InvalidRefreshToken, reuse.ErrorCode);

        // Even the legitimate newest token no longer works.
        await Assert.ThrowsAsync<UnauthorizedException>(() => RefreshAsync(refreshed.RefreshToken));

        var tokens = await _context.QueryAsync(db => db.RefreshTokens.Where(t => t.UserId == userId).ToListAsync());
        Assert.All(tokens, token => Assert.NotNull(token.RevokedAtUtc));
        Assert.Contains(tokens, token => token.RevokedReason == RefreshTokenRevokeReasons.ReuseDetected);
        Assert.True(await _context.QueryAsync(db =>
            db.AuditLogs.AnyAsync(a => a.Action == AuditAction.RefreshTokenReuseDetected && a.UserId == userId)));
    }

    [Fact]
    public async Task RefreshToken_Expired_IsRejected()
    {
        await _context.CreateUserAsync("agent6", Roles.ShippingAgent);
        var login = await LoginAsync("agent6");

        _context.Clock.Advance(TimeSpan.FromDays(8));

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() => RefreshAsync(login.RefreshToken));
        Assert.Equal(ErrorCodes.InvalidRefreshToken, exception.ErrorCode);
    }

    [Fact]
    public async Task RefreshToken_ForDeactivatedUser_IsRejected()
    {
        var userId = await _context.CreateUserAsync("agent7", Roles.ShippingAgent);
        var login = await LoginAsync("agent7");

        await using (var scope = _context.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId.ToString());
            user!.IsActive = false;
            await userManager.UpdateAsync(user);
        }

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => RefreshAsync(login.RefreshToken));
        Assert.Equal(ErrorCodes.AccountDisabled, exception.ErrorCode);
    }

    [Fact]
    public async Task RevokeToken_SignsOutTheDevice_AndAudits()
    {
        var userId = await _context.CreateUserAsync("agent8", Roles.ShippingAgent);
        var login = await LoginAsync("agent8");

        await using (var scope = _context.CreateScope())
        {
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            await authService.RevokeTokenAsync(new RevokeTokenRequest { RefreshToken = login.RefreshToken }, CancellationToken.None);

            // Idempotent: a second call does nothing and does not fail.
            await authService.RevokeTokenAsync(new RevokeTokenRequest { RefreshToken = login.RefreshToken }, CancellationToken.None);
        }

        await Assert.ThrowsAsync<UnauthorizedException>(() => RefreshAsync(login.RefreshToken));

        var revocations = await _context.QueryAsync(db =>
            db.AuditLogs.CountAsync(a => a.Action == AuditAction.RefreshTokenRevoked && a.UserId == userId));
        Assert.Equal(1, revocations);
    }

    [Fact]
    public async Task ChangePassword_ChangesStamp_RevokesOtherSessions_AndNewPasswordWorks()
    {
        var userId = await _context.CreateUserAsync("agent9", Roles.ShippingAgent);
        var otherDevice = await LoginAsync("agent9");
        var stampBefore = await _context.QueryAsync(db => db.Users.Where(u => u.Id == userId).Select(u => u.SecurityStamp).SingleAsync());

        AuthResponse changed;
        await using (var scope = _context.CreateScope())
        {
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            changed = await authService.ChangePasswordAsync(
                userId,
                new ChangePasswordRequest
                {
                    CurrentPassword = TestSettings.StrongPassword,
                    NewPassword = "New#Pass2027",
                    ConfirmNewPassword = "New#Pass2027"
                },
                CancellationToken.None);
        }

        var stampAfter = await _context.QueryAsync(db => db.Users.Where(u => u.Id == userId).Select(u => u.SecurityStamp).SingleAsync());
        Assert.NotEqual(stampBefore, stampAfter);

        // The other device is signed out; this device received a working new session.
        await Assert.ThrowsAsync<UnauthorizedException>(() => RefreshAsync(otherDevice.RefreshToken));
        Assert.False(string.IsNullOrWhiteSpace((await RefreshAsync(changed.RefreshToken)).AccessToken));

        await Assert.ThrowsAsync<UnauthorizedException>(() => LoginAsync("agent9"));
        Assert.Equal(userId, (await LoginAsync("agent9", "New#Pass2027")).User.Id);

        Assert.True(await _context.QueryAsync(db =>
            db.AuditLogs.AnyAsync(a => a.Action == AuditAction.PasswordChanged && a.UserId == userId)));
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_ReturnsValidationErrorOnCurrentPassword()
    {
        var userId = await _context.CreateUserAsync("agent10", Roles.ShippingAgent);

        await using var scope = _context.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => authService.ChangePasswordAsync(
            userId,
            new ChangePasswordRequest
            {
                CurrentPassword = "Wrong#Pass2026",
                NewPassword = "New#Pass2027",
                ConfirmNewPassword = "New#Pass2027"
            },
            CancellationToken.None));

        Assert.True(exception.Errors.ContainsKey("currentPassword"));
    }

    [Fact]
    public async Task SystemAdmin_ReceivesEveryPermission()
    {
        await _context.CreateUserAsync("root", Roles.SystemAdmin);

        var response = await LoginAsync("root");

        Assert.Equal(Permissions.All.Count, response.User.Permissions.Count);
    }
}
