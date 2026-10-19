using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ShippingERP.API.Extensions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.Common.Security;
using ShippingERP.Application.Interfaces.Identity;
using ShippingERP.Infrastructure.Identity;

namespace ShippingERP.API.Security;

/// <summary>
/// JWT bearer authentication. Settings come from the "Jwt" section (JwtOptions), read when the first
/// request arrives, so environment variables and test settings can override them.
/// </summary>
public static class JwtAuthenticationSetup
{
    public const string TokenExpiredHeader = "X-Token-Expired";

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptionsAccessor) =>
            {
                var jwtOptions = jwtOptionsAccessor.Value;

                // Keep the short claim names from the token ("sub", "role", "permission").
                bearerOptions.MapInboundClaims = false;

                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AppClaimTypes.UserName,
                    RoleClaimType = AppClaimTypes.Role
                };

                bearerOptions.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidateUserStateAsync,
                    OnChallenge = WriteUnauthorizedResponseAsync
                };
            });

        return services;
    }

    /// <summary>
    /// Runs after the signature and lifetime are valid. Rejects the token when the user no longer exists,
    /// was deactivated, or the security stamp changed (password, roles, branch or role permissions changed).
    /// Uses a short cache, so it does not hit the database on every request.
    /// </summary>
    private static async Task ValidateUserStateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var userIdValue = principal?.FindFirst(AppClaimTypes.UserId)?.Value;
        var securityStamp = principal?.FindFirst(AppClaimTypes.SecurityStamp)?.Value;

        if (!long.TryParse(userIdValue, out var userId) || securityStamp is null)
        {
            context.Fail("The token does not contain the required claims.");
            return;
        }

        var sessionCache = context.HttpContext.RequestServices.GetRequiredService<IUserSessionCache>();
        var state = await sessionCache.GetAsync(userId, context.HttpContext.RequestAborted);

        if (state is null || !state.IsActive)
        {
            context.Fail("The user does not exist or is deactivated.");
            return;
        }

        if (!string.Equals(state.SecurityStamp, securityStamp, StringComparison.Ordinal))
        {
            context.Fail(new StaleSecurityTokenException());
        }
    }

    /// <summary>
    /// Writes the standard JSON error for 401.
    /// TOKEN_EXPIRED = call POST /api/v1/auth/refresh-token and retry; UNAUTHORIZED = sign in again.
    /// </summary>
    private static async Task WriteUnauthorizedResponseAsync(JwtBearerChallengeContext context)
    {
        // Stop the default handler from writing its own empty response.
        context.HandleResponse();

        var tokenExpired = context.AuthenticateFailure is SecurityTokenExpiredException or StaleSecurityTokenException;

        context.Response.Headers.WWWAuthenticate = "Bearer";
        if (tokenExpired)
        {
            context.Response.Headers[TokenExpiredHeader] = "true";
        }

        var response = ApiResponse<object>.Fail(
            tokenExpired ? ErrorCodes.TokenExpired : ErrorCodes.Unauthorized,
            tokenExpired
                ? "انتهت صلاحية رمز الدخول. يرجى تحديث الجلسة."
                : "يجب تسجيل الدخول للوصول إلى هذا المورد.",
            null,
            context.HttpContext.GetTraceId());

        await context.HttpContext.WriteApiResponseAsync(StatusCodes.Status401Unauthorized, response);
    }
}
