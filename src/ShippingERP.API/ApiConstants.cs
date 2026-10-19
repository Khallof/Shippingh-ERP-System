namespace ShippingERP.API;

/// <summary>
/// Names shared between service registration, the request pipeline and tests.
/// Keeping them in one place avoids "magic strings" spread across files.
/// </summary>
public static class ApiConstants
{
    /// <summary>Named CORS policy used by app.UseCors(...).</summary>
    public const string CorsPolicyName = "ShippingErpCorsPolicy";

    /// <summary>Health checks with this tag are executed by /health/ready.</summary>
    public const string ReadyHealthCheckTag = "ready";

    /// <summary>Name of the SQL Server (EF Core) health check.</summary>
    public const string DatabaseHealthCheckName = "database";

    /// <summary>Rate-limit policy for sign-in, refresh, revoke and change-password (per client IP).</summary>
    public const string AuthRateLimitPolicy = "auth";
}
