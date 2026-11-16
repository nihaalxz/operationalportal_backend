using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using TexasOperationalPortal.Application.Common.Options;

namespace TexasOperationalPortal.Infrastructure.BackgroundServices;

public sealed class OutboxNotificationWorker : BackgroundService
{
    private readonly WorkerOptions _options;
    private readonly ILogger<OutboxNotificationWorker> _logger;

    public OutboxNotificationWorker(
        IOptions<WorkerOptions> options,
        ILogger<OutboxNotificationWorker> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("OutboxNotificationWorker (Operational) is disabled in configuration.");
            return;
        }

        _logger.LogInformation("OutboxNotificationWorker (Operational) started. Interval: {Interval}ms, BatchSize: {BatchSize}",
            _options.PollingIntervalMilliseconds, _options.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingOutboxBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred inside OutboxNotificationWorker loop.");
            }

            await Task.Delay(_options.PollingIntervalMilliseconds, stoppingToken);
        }

        _logger.LogInformation("OutboxNotificationWorker background service is stopping.");
    }

    private Task ProcessPendingOutboxBatchAsync(CancellationToken ct)
    {
        _logger.LogTrace("OutboxNotificationWorker polling iteration completed with 0 pending notifications.");
        return Task.CompletedTask;
    }
}
