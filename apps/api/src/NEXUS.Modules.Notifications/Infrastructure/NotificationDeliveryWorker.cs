using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Notifications.Application;

namespace NEXUS.Modules.Notifications.Infrastructure;

public sealed class NotificationDeliveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationDeliveryOptions _options;
    private readonly ILogger<NotificationDeliveryWorker> _logger;

    public NotificationDeliveryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationDeliveryOptions> options,
        ILogger<NotificationDeliveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.PollInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<NotificationDeliveryProcessor>();
                var count = await processor.DispatchBatchAsync(stoppingToken);
                if (count > 0) _logger.LogInformation("Processed notification delivery batch. Count={Count}", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                // Error details are deliberately omitted because provider/database failures may contain sensitive values.
                _logger.LogError("Notification delivery worker run failed; leased items will be recovered after expiry.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
