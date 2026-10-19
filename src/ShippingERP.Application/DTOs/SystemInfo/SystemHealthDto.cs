namespace ShippingERP.Application.DTOs.SystemInfo;

/// <summary>
/// Response body of GET /api/v1/system/health.
/// </summary>
public sealed class SystemHealthDto
{
    public string ApplicationName { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string Environment { get; init; } = string.Empty;

    /// <summary>Overall status: Healthy, Degraded or Unhealthy.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Database status: Healthy, Degraded, Unhealthy or Unknown.</summary>
    public string DatabaseStatus { get; init; } = string.Empty;

    /// <summary>
    /// Why the database check failed or succeeded (server/database names and SQL error).
    /// Returned in Development only; null in other environments so server details are never exposed.
    /// </summary>
    public string? DatabaseMessage { get; init; }

    /// <summary>Current server time in UTC (ISO 8601, ends with "Z").</summary>
    public DateTime ServerTimeUtc { get; init; }
}
