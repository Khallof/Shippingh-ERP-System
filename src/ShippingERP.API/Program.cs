using Serilog;
using ShippingERP.API.Extensions;
using ShippingERP.Application;
using ShippingERP.Infrastructure;

// Temporary logger used only until the host is built, so startup errors are still logged.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting ShippingERP API");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog: reads sinks and levels from the "Serilog" section of appsettings.json.
    builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Layers (order does not matter for registration).
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApiServices(builder.Configuration, builder.Environment);

    var app = builder.Build();

    await app.ApplyDatabaseMigrationsAsync();
    await app.SeedDatabaseAsync();

    app.UseApiPipeline();

    await app.RunAsync();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    // HostAbortedException is thrown on purpose by "dotnet ef" tooling; it is not an error.
    Log.Fatal(exception, "ShippingERP API terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Makes the auto-generated Program class public so integration tests can use WebApplicationFactory&lt;Program&gt;.
/// </summary>
public partial class Program
{
}
