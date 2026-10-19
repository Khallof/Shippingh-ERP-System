using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Auth;
using ShippingERP.Application.Interfaces;
using ShippingERP.Application.Interfaces.Identity;
using ShippingERP.Domain.Entities.Identity;
using ShippingERP.Domain.Enums;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.Infrastructure.Identity;

/// <summary>
/// Sign-in, refresh-token rotation, sign-out and password change.
///
/// Passwords are checked ONLY with ASP.NET Core Identity (UserManager.CheckPasswordAsync / PasswordHasher).
/// Wrong passwords count towards Identity's lockout (5 attempts = 15 minutes).
/// </summary>
public sealed class AuthService : IAuthService
{
    private const string UserEntityName = "User";
    private const string RefreshTokenEntityName = "RefreshToken";

    /// <summary>
    /// Hash of a random password, used to spend the same time hashing when the user name does not exist,
    /// so response times do not reveal which user names are valid.
    /// </summary>
    private static readonly Lazy<string> DummyPasswordHash = new(() =>
        new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString("N")));

    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IUserSessionCache _sessionCache;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentUserService _currentUserService;
    private readonly AuditLogWriter _auditLog;
    private readonly UserAccessReader _accessReader;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IUserSessionCache sessionCache,
        IDateTimeProvider dateTimeProvider,
        ICurrentUserService currentUserService,
        AuditLogWriter auditLog,
        UserAccessReader accessReader,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _tokenService = tokenService;
        _sessionCache = sessionCache;
        _dateTimeProvider = dateTimeProvider;
        _currentUserService = currentUserService;
        _auditLog = auditLog;
        _accessReader = accessReader;
        _logger = logger;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var login = request.UserNameOrEmail.Trim();

        var user = await _userManager.FindByNameAsync(login) ?? await _userManager.FindByEmailAsync(login);

        if (user is null)
        {
            // Same hashing cost as a real check, so attackers cannot detect valid user names by timing.
            _userManager.PasswordHasher.VerifyHashedPassword(new ApplicationUser(), DummyPasswordHash.Value, request.Password);

            await AuditFailedLoginAsync(null, login, "UnknownUser", cancellationToken);
            throw InvalidCredentials();
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            await AuditFailedLoginAsync(user, login, "LockedOut", cancellationToken);
            throw AccountLocked(user);
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            // Increments AccessFailedCount; the 5th failure sets LockoutEnd.
            await _userManager.AccessFailedAsync(user);
            var lockedNow = await _userManager.IsLockedOutAsync(user);

            await AuditFailedLoginAsync(user, login, lockedNow ? "InvalidPasswordLockedOut" : "InvalidPassword", cancellationToken);

            if (lockedNow)
            {
                throw AccountLocked(user);
            }

            throw InvalidCredentials();
        }

        // Checked AFTER the password, so the "disabled" message is only shown to someone who knows the password.
        if (!user.IsActive)
        {
            await AuditFailedLoginAsync(user, login, "UserInactive", cancellationToken);
            throw new ForbiddenException(
                ErrorCodes.AccountDisabled,
                "تم إيقاف هذا الحساب. يرجى التواصل مع مدير النظام.");
        }

        var response = await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            if (user.AccessFailedCount > 0)
            {
                await _userManager.ResetAccessFailedCountAsync(user);
            }

            user.LastLoginAtUtc = _dateTimeProvider.UtcNow;
            (await _userManager.UpdateAsync(user)).ThrowIfFailed();

            var authResponse = await IssueTokensAsync(user, Guid.NewGuid(), ct);

            _auditLog.Add(
                AuditAction.Login,
                userId: user.Id,
                userName: user.UserName,
                entityName: UserEntityName,
                entityId: user.Id.ToString(),
                actorUserId: user.Id);

            await _dbContext.SaveChangesAsync(ct);
            return authResponse.Response;
        }, cancellationToken);

        _logger.LogInformation("User {UserId} signed in", user.Id);
        return response;
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        var tokenHash = _tokenService.HashRefreshToken(request.RefreshToken);

        var storedToken = await _dbContext.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            throw InvalidRefreshToken();
        }

        if (storedToken.IsRevoked)
        {
            if (storedToken.RevokedReason == RefreshTokenRevokeReasons.Rotated)
            {
                // A token that was already exchanged is being used again: either the client has a bug,
                // or the token was stolen. Revoke the whole session (token family) to be safe.
                await HandleRefreshTokenReuseAsync(storedToken, utcNow, cancellationToken);
            }

            throw InvalidRefreshToken();
        }

        if (storedToken.IsExpired(utcNow))
        {
            throw InvalidRefreshToken();
        }

        var user = storedToken.User;

        if (!user.IsActive)
        {
            storedToken.Revoke(utcNow, _currentUserService.IpAddress, RefreshTokenRevokeReasons.UserDeactivated);
            await _dbContext.SaveChangesAsync(cancellationToken);

            throw new ForbiddenException(
                ErrorCodes.AccountDisabled,
                "تم إيقاف هذا الحساب. يرجى التواصل مع مدير النظام.");
        }

        try
        {
            return await _dbContext.ExecuteInTransactionAsync(async ct =>
            {
                var issued = await IssueTokensAsync(user, storedToken.FamilyId, ct);

                // Rotation: the old token is used up and points to its replacement.
                storedToken.Revoke(
                    utcNow,
                    _currentUserService.IpAddress,
                    RefreshTokenRevokeReasons.Rotated,
                    replacedBy: issued.RefreshToken);

                await _dbContext.SaveChangesAsync(ct);
                return issued.Response;
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Two requests tried to use the same refresh token at the same moment; only the first one wins.
            _logger.LogWarning("Concurrent use of refresh token {TokenId} for user {UserId}", storedToken.Id, user.Id);
            throw InvalidRefreshToken();
        }
    }

    public async Task RevokeTokenAsync(RevokeTokenRequest request, CancellationToken cancellationToken)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        var tokenHash = _tokenService.HashRefreshToken(request.RefreshToken);

        var storedToken = await _dbContext.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        // Idempotent: signing out with an unknown, expired or already revoked token simply succeeds.
        if (storedToken is null || !storedToken.IsActive(utcNow))
        {
            return;
        }

        storedToken.Revoke(utcNow, _currentUserService.IpAddress, RefreshTokenRevokeReasons.Logout);

        _auditLog.Add(
            AuditAction.RefreshTokenRevoked,
            userId: storedToken.UserId,
            userName: storedToken.User.UserName,
            entityName: RefreshTokenEntityName,
            entityId: storedToken.Id.ToString(),
            details: new { reason = RefreshTokenRevokeReasons.Logout },
            actorUserId: _currentUserService.UserId ?? storedToken.UserId);

        // One SaveChanges = one transaction (token update + audit row).
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthResponse> ChangePasswordAsync(
        long userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString()) ?? throw new UnauthorizedException();

        if (!user.IsActive)
        {
            throw new ForbiddenException(
                ErrorCodes.AccountDisabled,
                "تم إيقاف هذا الحساب. يرجى التواصل مع مدير النظام.");
        }

        var utcNow = _dateTimeProvider.UtcNow;

        var response = await _dbContext.ExecuteInTransactionAsync(async ct =>
        {
            // Verifies the current password, validates and hashes the new one, and changes the
            // security stamp (which invalidates every access token issued before).
            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

            if (!result.Succeeded &&
                result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
            {
                throw new RequestValidationException(new Dictionary<string, string[]>
                {
                    ["currentPassword"] = new[] { "كلمة المرور الحالية غير صحيحة." }
                });
            }

            result.ThrowIfFailed(passwordField: "newPassword");

            // Sign out every device, then give THIS device a new session.
            await RevokeAllActiveTokensAsync(user.Id, RefreshTokenRevokeReasons.PasswordChanged, utcNow, ct);
            var issued = await IssueTokensAsync(user, Guid.NewGuid(), ct);

            _auditLog.Add(
                AuditAction.PasswordChanged,
                userId: user.Id,
                userName: user.UserName,
                entityName: UserEntityName,
                entityId: user.Id.ToString(),
                actorUserId: user.Id);

            await _dbContext.SaveChangesAsync(ct);
            return issued.Response;
        }, cancellationToken);

        _sessionCache.Invalidate(user.Id);
        _logger.LogInformation("User {UserId} changed the password", user.Id);

        return response;
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
            ?? throw new UnauthorizedException();

        var access = await _accessReader.GetAsync(user.Id, cancellationToken);
        return ToCurrentUserDto(user, access);
    }

    /// <summary>
    /// Creates a new access token and a new refresh token (added to the DbContext, not saved).
    /// </summary>
    private async Task<IssuedTokens> IssueTokensAsync(ApplicationUser user, Guid familyId, CancellationToken cancellationToken)
    {
        var access = await _accessReader.GetAsync(user.Id, cancellationToken);

        var accessToken = _tokenService.CreateAccessToken(new TokenUserInfo(
            user.Id,
            user.UserName ?? string.Empty,
            user.FullName,
            user.BranchId,
            user.SecurityStamp ?? string.Empty,
            access.Roles,
            access.Permissions));

        var refreshToken = _tokenService.CreateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshToken.TokenHash,
            FamilyId = familyId,
            ExpiresAtUtc = refreshToken.ExpiresAtUtc,
            CreatedByIp = Truncate(_currentUserService.IpAddress, 64),
            UserAgent = Truncate(_currentUserService.UserAgent, 512)
        };

        _dbContext.RefreshTokens.Add(refreshTokenEntity);

        var response = new AuthResponse
        {
            AccessToken = accessToken.Token,
            AccessTokenExpiresAtUtc = accessToken.ExpiresAtUtc,
            RefreshToken = refreshToken.RawToken,
            RefreshTokenExpiresAtUtc = refreshToken.ExpiresAtUtc,
            User = ToCurrentUserDto(user, access)
        };

        return new IssuedTokens(response, refreshTokenEntity);
    }

    private async Task HandleRefreshTokenReuseAsync(RefreshToken reusedToken, DateTime utcNow, CancellationToken cancellationToken)
    {
        var familyTokens = await _dbContext.RefreshTokens
            .Where(token => token.FamilyId == reusedToken.FamilyId && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in familyTokens)
        {
            token.Revoke(utcNow, _currentUserService.IpAddress, RefreshTokenRevokeReasons.ReuseDetected);
        }

        _auditLog.Add(
            AuditAction.RefreshTokenReuseDetected,
            succeeded: false,
            userId: reusedToken.UserId,
            userName: reusedToken.User.UserName,
            entityName: RefreshTokenEntityName,
            entityId: reusedToken.Id.ToString(),
            details: new { familyId = reusedToken.FamilyId, revokedTokens = familyTokens.Count },
            actorUserId: reusedToken.UserId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Refresh token reuse detected for user {UserId}; {Count} active token(s) of the session were revoked",
            reusedToken.UserId,
            familyTokens.Count);
    }

    private async Task RevokeAllActiveTokensAsync(long userId, string reason, DateTime utcNow, CancellationToken cancellationToken)
    {
        var activeTokens = await _dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null && token.ExpiresAtUtc > utcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(utcNow, _currentUserService.IpAddress, reason);
        }
    }

    private async Task AuditFailedLoginAsync(
        ApplicationUser? user,
        string attemptedLogin,
        string reason,
        CancellationToken cancellationToken)
    {
        _auditLog.Add(
            AuditAction.LoginFailed,
            succeeded: false,
            userId: user?.Id,
            userName: user?.UserName ?? attemptedLogin,
            entityName: UserEntityName,
            entityId: user?.Id.ToString(),
            details: new { reason });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Failed sign-in for {Login}: {Reason}", attemptedLogin, reason);
    }

    private ForbiddenException AccountLocked(ApplicationUser user)
    {
        var minutes = user.LockoutEnd.HasValue
            ? Math.Max(1, (int)Math.Ceiling((user.LockoutEnd.Value.UtcDateTime - _dateTimeProvider.UtcNow).TotalMinutes))
            : 15;

        return new ForbiddenException(
            ErrorCodes.AccountLocked,
            $"تم قفل الحساب مؤقتاً بسبب محاولات دخول فاشلة متكررة. حاول مرة أخرى بعد {minutes} دقيقة.");
    }

    private static UnauthorizedException InvalidCredentials() =>
        new(ErrorCodes.InvalidCredentials, "اسم المستخدم أو كلمة المرور غير صحيحة.");

    private static UnauthorizedException InvalidRefreshToken() =>
        new(ErrorCodes.InvalidRefreshToken, "انتهت الجلسة. يرجى تسجيل الدخول مرة أخرى.");

    private static CurrentUserDto ToCurrentUserDto(ApplicationUser user, UserAccess access) => new()
    {
        Id = user.Id,
        UserName = user.UserName ?? string.Empty,
        Email = user.Email,
        FullName = user.FullName,
        ArabicFullName = user.ArabicFullName,
        BranchId = user.BranchId,
        EmployeeCode = user.EmployeeCode,
        LastLoginAtUtc = user.LastLoginAtUtc,
        Roles = access.Roles,
        Permissions = access.Permissions
    };

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];

    private sealed record IssuedTokens(AuthResponse Response, RefreshToken RefreshToken);
}
