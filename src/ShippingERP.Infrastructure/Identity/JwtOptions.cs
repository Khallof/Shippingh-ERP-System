namespace ShippingERP.Infrastructure.Identity;

/// <summary>
/// "Jwt" section of the configuration.
///
/// SecretKey is NOT in appsettings.json. Provide it through the environment variable Jwt__SecretKey
/// (or dotnet user-secrets in Development): at least 32 random characters. Changing it signs everybody out.
/// The API refuses to start when it is missing or too short.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSecretKeyLength = 32;

    public string Issuer { get; set; } = "ShippingERP";

    public string Audience { get; set; } = "ShippingERP.Clients";

    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Access token lifetime. Short on purpose (default 15 minutes).</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Refresh token lifetime (default 7 days). Every refresh issues a new token with a new lifetime.</summary>
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>
    /// How long the (IsActive, SecurityStamp) check of a user is cached per API instance (default 30 seconds).
    /// Changes made through this API clear the cache immediately; the delay only matters when several
    /// API instances run behind a load balancer.
    /// </summary>
    public int UserStateCacheSeconds { get; set; } = 30;

    /// <summary>
    /// Step 8 (browser sessions): the httpOnly refresh-token cookie is marked Secure (HTTPS only). Browsers accept
    /// Secure cookies on http://localhost, so keep true; set false only for an HTTP test server on another host name.
    /// </summary>
    public bool RefreshCookieSecure { get; set; } = true;
}
