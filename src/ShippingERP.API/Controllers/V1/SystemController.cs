using System.Reflection;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.SystemInfo;
using ShippingERP.Application.Interfaces;

namespace ShippingERP.API.Controllers.V1;

/// <summary>
/// System information endpoints (معلومات النظام).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system")]
[AllowAnonymous]
public sealed class SystemController : ApiControllerBase
{
    private readonly HealthCheckService _healthCheckService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IHostEnvironment _environment;

    public SystemController(
        HealthCheckService healthCheckService,
        IDateTimeProvider dateTimeProvider,
        IHostEnvironment environment)
    {
        _healthCheckService = healthCheckService;
        _dateTimeProvider = dateTimeProvider;
        _environment = environment;
    }

    /// <summary>
    /// Returns the application status, database status and the current server time in UTC.
    /// </summary>
    /// <remarks>
    /// Always returns HTTP 200 so the frontend can display the status.
    /// For load balancers and Docker use /health/ready, which returns 503 when the database is down.
    /// </remarks>
    /// <param name="cancellationToken">Cancelled automatically when the client disconnects.</param>
    [HttpGet("health")]
    [ProducesResponseType(typeof(ApiResponse<SystemHealthDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SystemHealthDto>>> GetHealth(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(cancellationToken);

        var hasDatabaseEntry = report.Entries.TryGetValue(ApiConstants.DatabaseHealthCheckName, out var databaseEntry);

        var dto = new SystemHealthDto
        {
            ApplicationName = "ShippingERP.API",
            Version = GetApplicationVersion(),
            Environment = _environment.EnvironmentName,
            Status = report.Status.ToString(),
            DatabaseStatus = hasDatabaseEntry ? databaseEntry.Status.ToString() : "Unknown",
            DatabaseMessage = hasDatabaseEntry && _environment.IsDevelopment() ? databaseEntry.Description : null,
            ServerTimeUtc = _dateTimeProvider.UtcNow
        };

        var message = report.Status == HealthStatus.Healthy
            ? "النظام يعمل بشكل طبيعي"
            : "النظام يعمل مع وجود مشاكل";

        return OkResponse(dto, message);
    }

    private static string GetApplicationVersion()
    {
        var assembly = typeof(SystemController).Assembly;

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        // Strip the "+commitHash" suffix that newer SDKs append.
        return informationalVersion?.Split('+')[0]
               ?? assembly.GetName().Version?.ToString()
               ?? "1.0.0";
    }
}
