using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShippingERP.Infrastructure.Persistence;

namespace ShippingERP.API.HealthChecks;

/// <summary>
/// Checks the SQL Server connection and, unlike the built-in AddDbContextCheck,
/// reports WHY it failed (server not found, login failed, database does not exist...).
/// The reason appears in the logs, in /health/ready and (Development only) in /api/v1/system/health.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(AppDbContext dbContext, ILogger<DatabaseHealthCheck> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _dbContext.Database.GetConnectionString();
        var target = DescribeTarget(connectionString);

        try
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);

            try
            {
                await using var command = _dbContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync(cancellationToken);
            }
            finally
            {
                await _dbContext.Database.CloseConnectionAsync();
            }

            return HealthCheckResult.Healthy($"Connected to {target}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var reason = exception is SqlException sqlException
                ? $"SQL error {sqlException.Number}: {sqlException.Message}"
                : $"{exception.GetType().Name}: {exception.Message}";

            _logger.LogWarning(exception, "Database health check failed for {Target}. {Reason}", target, reason);

            return new HealthCheckResult(
                context.Registration.FailureStatus,
                description: $"Cannot connect to {target}. {reason}",
                exception: exception);
        }
    }

    /// <summary>Server and database names only — never the password.</summary>
    private static string DescribeTarget(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "(no connection string)";
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return $"server '{builder.DataSource}', database '{builder.InitialCatalog}'";
        }
        catch (ArgumentException)
        {
            return "(invalid connection string)";
        }
    }
}
