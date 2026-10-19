using Microsoft.Extensions.Options;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Infrastructure.Accounting;

namespace ShippingERP.API.BackgroundJobs;

/// <summary>
/// Automatic posting dispatcher (step 7): every Accounting:DispatcherIntervalSeconds it posts the waiting outbox
/// messages (invoices, payments, COD, vouchers, transfers, settlements...) as journal entries, in a new DI scope each
/// run. Several API instances can run it at the same time: the unique EventId / SourceKey indexes make sure nothing is
/// posted twice. Errors are logged and never stop the application; a failed message is retried on the next runs
/// (up to Accounting:MaxAutomaticAttempts), then waits for an accountant (posting-events/{id}/retry).
/// </summary>
public sealed class AccountingPostingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AccountingOptions _options;
    private readonly ILogger<AccountingPostingBackgroundService> _logger;

    public AccountingPostingBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<AccountingOptions> options,
        ILogger<AccountingPostingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.DispatcherEnabled)
        {
            _logger.LogInformation("Accounting posting dispatcher is disabled (Accounting:DispatcherEnabled = false)");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.DispatcherStartDelaySeconds)), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.DispatcherIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var postings = scope.ServiceProvider.GetRequiredService<IAccountingPostingService>();
                var result = await postings.ProcessPendingAsync(Math.Clamp(_options.DispatcherBatchSize, 1, 1000), stoppingToken);

                if (result.Processed > 0)
                {
                    _logger.LogInformation(
                        "Accounting postings: {Posted} posted, {Skipped} without effect, {Duplicates} duplicates, {Failed} failed, {Remaining} waiting",
                        result.Posted, result.Skipped, result.Duplicates, result.Failed, result.Remaining);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Accounting posting run failed; it runs again in {Interval}", interval);
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
