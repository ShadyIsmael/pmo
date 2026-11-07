using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NEXUS.Modules.ProjectManagement.Application;

/// <summary>Applies approved project-manager handovers on their effective date.</summary>
public sealed class ProjectManagerHandoverScheduler : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectManagerHandoverScheduler> _logger;

    public ProjectManagerHandoverScheduler(
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectManagerHandoverScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var handovers = scope.ServiceProvider.GetRequiredService<IProjectHandoverRequestService>();
                var processed = await handovers.ProcessDueApprovedHandoversAsync(stoppingToken);
                if (processed > 0)
                    _logger.LogInformation("Applied {HandoverCount} project-manager handover(s) on or after their effective date", processed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Project-manager handover scheduler run failed; due requests will be retried");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
