using Microsoft.Extensions.Options;
using ShippingERP.Application.Interfaces.Billing;

namespace ShippingERP.API.BackgroundJobs;

/// <summary>Billing section of appsettings.json.</summary>
public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>Run the overdue check in the background (disable it in tests or on secondary instances).</summary>
    public bool OverdueCheckEnabled { get; set; } = true;

    /// <summary>Minutes between two checks (default 60).</summary>
    public int OverdueCheckIntervalMinutes { get; set; } = 60;

    /// <summary>Delay after start-up before the first check (default 2 minutes), so the start-up stays fast.</summary>
    public int OverdueCheckStartDelaySeconds { get; set; } = 120;
}

/// <summary>
/// Marks issued / partially paid invoices whose due date has passed as Overdue, every hour.
/// Errors are logged and never stop the application (the next run tries again). The aging report does not
/// depend on this job: it ages invoices by their due date directly.
/// </summary>
public sealed class InvoiceOverdueBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BillingOptions _options;
    private readonly ILogger<InvoiceOverdueBackgroundService> _logger;

    public InvoiceOverdueBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<BillingOptions> options,
        ILogger<InvoiceOverdueBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.OverdueCheckEnabled)
        {
            _logger.LogInformation("Invoice overdue check is disabled (Billing:OverdueCheckEnabled = false)");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.OverdueCheckStartDelaySeconds)), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.OverdueCheckIntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var invoices = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
                var count = await invoices.RefreshOverdueAsync(stoppingToken);
                if (count > 0)
                {
                    _logger.LogInformation("Invoice overdue check: {Count} invoice(s) marked Overdue", count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Invoice overdue check failed; it will run again in {Interval}", interval);
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
