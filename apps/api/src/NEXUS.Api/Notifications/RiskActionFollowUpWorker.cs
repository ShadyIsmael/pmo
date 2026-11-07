using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NEXUS.Api.Notifications;

/// <summary>
/// Polls the durable Risk Action state and resumes overdue notifications after restart.
/// The notification primary key provides cross-instance delivery idempotency.
/// </summary>
public sealed class RiskActionFollowUpWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RiskActionFollowUpWorker> _logger;

    public RiskActionFollowUpWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<RiskActionFollowUpWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(PollInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<RiskActionFollowUpProcessor>();
            var processed = await processor.ProcessDueAsync(cancellationToken);
            if (processed > 0)
                _logger.LogInformation("Processed Risk Action follow-up notification batch. Count={Count}", processed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            // A later poll rereads durable actions, settings, and Notification rows.
            _logger.LogError("Risk Action follow-up worker run failed; outstanding reminders will be retried.");
        }
    }
}
