using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShippingERP.API;
using ShippingERP.Application.Interfaces.Identity;

namespace ShippingERP.Tests.Common;

/// <summary>
/// Starts the real API in memory for integration tests, WITHOUT SQL Server:
/// - the database health check is replaced by a fake "healthy" check,
/// - seeding and the background jobs (invoice overdue check, accounting dispatcher) are disabled,
/// - the per-request user check (IsActive + security stamp) uses <see cref="FakeUserSessionCache"/>,
///   so tests can send real signed JWTs to protected endpoints.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public FakeUserSessionCache SessionCache { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("Seed:Enabled", "false");
        builder.UseSetting("Swagger:Enabled", "true");
        builder.UseSetting("Jwt:SecretKey", TestSettings.JwtSecretKey);
        builder.UseSetting("RateLimiting:AuthPermitLimit", "1000");
        builder.UseSetting("Billing:OverdueCheckEnabled", "false");
        builder.UseSetting("Billing:AutoInvoiceShipments", "false");
        builder.UseSetting("Accounting:DispatcherEnabled", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserSessionCache>();
            services.AddSingleton<IUserSessionCache>(SessionCache);

            services.Configure<HealthCheckServiceOptions>(options =>
            {
                var databaseCheck = options.Registrations
                    .FirstOrDefault(r => r.Name == ApiConstants.DatabaseHealthCheckName);

                if (databaseCheck is not null)
                {
                    options.Registrations.Remove(databaseCheck);
                }

                options.Registrations.Add(new HealthCheckRegistration(
                    ApiConstants.DatabaseHealthCheckName,
                    _ => new AlwaysHealthyCheck(),
                    failureStatus: null,
                    tags: new[] { ApiConstants.ReadyHealthCheckTag }));
            });
        });
    }

    private sealed class AlwaysHealthyCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Fake database check for tests"));
        }
    }
}
