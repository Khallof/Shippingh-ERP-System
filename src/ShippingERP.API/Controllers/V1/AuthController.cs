using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ShippingERP.API.Extensions;
using ShippingERP.Application.Common.Exceptions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Auth;
using ShippingERP.Application.Interfaces;
using ShippingERP.Application.Interfaces.Identity;
using ShippingERP.Infrastructure.Identity;

namespace ShippingERP.API.Controllers.V1;

/// <summary>
/// Sign-in and session management (تسجيل الدخول والجلسات).
///
/// Two ways to keep a session (step 8):
///   - API clients (Swagger, mobile apps): the refresh token is returned in the body and sent back to /refresh-token.
///   - Browsers (the React web app): send the header "X-Auth-Mode: cookie" to /login and /change-password. The refresh
///     token is then put in an httpOnly, Secure, SameSite=Strict cookie limited to /api/v1/auth/web and is NOT returned
///     in the body, so JavaScript (and an XSS attack) can never read it. The web app renews with POST /auth/web/refresh
///     and signs out with POST /auth/web/logout, both with the same header (a custom header cannot be sent by another
///     site without a CORS pre-flight, which is the CSRF protection). The access token stays in memory only.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController : ApiControllerBase
{
    /// <summary>Header that switches a request to the browser (cookie) session mode.</summary>
    public const string AuthModeHeader = "X-Auth-Mode";

    public const string CookieMode = "cookie";

    /// <summary>Name of the httpOnly refresh-token cookie.</summary>
    public const string RefreshCookieName = "erp_refresh";

    /// <summary>The cookie is sent by the browser only to the web session endpoints.</summary>
    public const string RefreshCookiePath = "/api/v1/auth/web";

    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUserService;
    private readonly JwtOptions _jwtOptions;

    public AuthController(IAuthService authService, ICurrentUserService currentUserService, IOptions<JwtOptions> jwtOptions)
    {
        _authService = authService;
        _currentUserService = currentUserService;
        _jwtOptions = jwtOptions.Value;
    }

    /// <summary>Signs in with user name (or e-mail) and password. Returns an access token and a refresh token.</summary>
    /// <remarks>
    /// Errors: 401 INVALID_CREDENTIALS, 403 ACCOUNT_DISABLED, 403 ACCOUNT_LOCKED (5 wrong passwords = 15 minutes),
    /// 429 TOO_MANY_REQUESTS (more than 10 calls per minute from one IP address).
    /// </remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiConstants.AuthRateLimitPolicy)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request, cancellationToken);
        return OkResponse(IsCookieMode() ? ToCookieSession(response) : response, "تم تسجيل الدخول بنجاح");
    }

    /// <summary>Exchanges a refresh token for a new access token AND a new refresh token.</summary>
    /// <remarks>
    /// Call this when any endpoint returns 401 TOKEN_EXPIRED. The old refresh token stops working immediately;
    /// presenting it again is treated as theft and signs out that whole session (401 INVALID_REFRESH_TOKEN).
    /// </remarks>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiConstants.AuthRateLimitPolicy)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService.RefreshTokenAsync(request, cancellationToken);
        return OkResponse(response, "تم تحديث الجلسة");
    }

    /// <summary>Signs out this device by revoking its refresh token.</summary>
    /// <remarks>
    /// Works without an access token (it may already be expired). Always returns 200, even for an unknown token,
    /// so it cannot be used to test whether a token exists. The access token stays valid until it expires
    /// (at most Jwt:AccessTokenMinutes), so the client must also delete it.
    /// </remarks>
    [HttpPost("revoke-token")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiConstants.AuthRateLimitPolicy)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<object>>> RevokeToken(
        [FromBody] RevokeTokenRequest request,
        CancellationToken cancellationToken)
    {
        await _authService.RevokeTokenAsync(request, cancellationToken);
        return OkMessage("تم تسجيل الخروج");
    }

    /// <summary>Changes the signed-in user's password.</summary>
    /// <remarks>
    /// Signs out every other device and returns a NEW token pair for this device: replace the stored tokens.
    /// Errors: 400 VALIDATION_ERROR with field currentPassword (wrong password) or newPassword (weak password).
    /// </remarks>
    [HttpPost("change-password")]
    [EnableRateLimiting(ApiConstants.AuthRateLimitPolicy)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException();
        var response = await _authService.ChangePasswordAsync(userId, request, cancellationToken);
        return OkResponse(IsCookieMode() ? ToCookieSession(response) : response, "تم تغيير كلمة المرور بنجاح");
    }

    /// <summary>Returns the signed-in user with current roles and permissions (for menus and screens).</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CurrentUserDto>>> Me(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new UnauthorizedException();
        var user = await _authService.GetCurrentUserAsync(userId, cancellationToken);
        return OkResponse(user);
    }

    // ------------------------------------------------------------------ browser session (httpOnly cookie)

    /// <summary>Browser session: exchanges the refresh-token COOKIE for a new access token (and a new cookie).</summary>
    /// <remarks>
    /// Needs the header "X-Auth-Mode: cookie" (400 otherwise) and the cookie set by /login (401 when it is missing,
    /// expired or already used). The body contains the access token only.
    /// </remarks>
    [HttpPost("web/refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiConstants.AuthRateLimitPolicy)]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> WebRefresh(CancellationToken cancellationToken)
    {
        EnsureCookieMode();

        var token = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new UnauthorizedException("INVALID_REFRESH_TOKEN", "انتهت الجلسة. سجّل الدخول من جديد.");
        }

        try
        {
            var response = await _authService.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = token }, cancellationToken);
            return OkResponse(ToCookieSession(response), "تم تحديث الجلسة");
        }
        catch (UnauthorizedException exception)
        {
            // Answered here (not by the exception middleware, which clears the headers) so the dead cookie is removed.
            DeleteRefreshCookie();
            return StatusCode(
                StatusCodes.Status401Unauthorized,
                ApiResponse<object>.Fail(exception.ErrorCode, exception.Message, null, HttpContext.GetTraceId()));
        }
    }

    /// <summary>Browser session: revokes the refresh-token cookie and deletes it. Always 200.</summary>
    [HttpPost("web/logout")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiConstants.AuthRateLimitPolicy)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> WebLogout(CancellationToken cancellationToken)
    {
        EnsureCookieMode();

        var token = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrWhiteSpace(token))
        {
            await _authService.RevokeTokenAsync(new RevokeTokenRequest { RefreshToken = token }, cancellationToken);
        }

        DeleteRefreshCookie();
        return OkMessage("تم تسجيل الخروج");
    }

    private bool IsCookieMode() =>
        string.Equals(Request.Headers[AuthModeHeader].ToString(), CookieMode, StringComparison.OrdinalIgnoreCase);

    private void EnsureCookieMode()
    {
        if (!IsCookieMode())
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                [AuthModeHeader] = new[] { "هذا الطلب خاص بجلسات المتصفح ويحتاج الترويسة X-Auth-Mode: cookie." }
            });
        }
    }

    /// <summary>Puts the refresh token in the httpOnly cookie and removes it from the body.</summary>
    private AuthResponse ToCookieSession(AuthResponse response)
    {
        Response.Cookies.Append(RefreshCookieName, response.RefreshToken, RefreshCookieOptions(response.RefreshTokenExpiresAtUtc));

        return new AuthResponse
        {
            TokenType = response.TokenType,
            AccessToken = response.AccessToken,
            AccessTokenExpiresAtUtc = response.AccessTokenExpiresAtUtc,
            RefreshToken = string.Empty,
            RefreshTokenExpiresAtUtc = response.RefreshTokenExpiresAtUtc,
            User = response.User
        };
    }

    private void DeleteRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, RefreshCookieOptions(null));

    private CookieOptions RefreshCookieOptions(DateTime? expiresAtUtc) => new()
    {
        HttpOnly = true,
        Secure = _jwtOptions.RefreshCookieSecure || Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = RefreshCookiePath,
        Expires = expiresAtUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc.Value, DateTimeKind.Utc)) : null,
        IsEssential = true
    };
}
