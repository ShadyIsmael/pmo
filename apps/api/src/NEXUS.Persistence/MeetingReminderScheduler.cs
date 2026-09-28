using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NEXUS.Modules.Meetings.Domain;

namespace NEXUS.Persistence;

/// <summary>
/// Creates delivery-agnostic pending reminder records. Delivery is intentionally out of scope.
/// </summary>
public sealed class MeetingReminderScheduler : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private const int MaximumReminderOffsetMinutes = MeetingReminderOffsets.OneDay;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MeetingReminderScheduler> _logger;

    public MeetingReminderScheduler(IServiceScopeFactory scopeFactory, ILogger<MeetingReminderScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken)) await RunOnceAsync(stoppingToken);
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            var windowEnd = now.AddMinutes(MaximumReminderOffsetMinutes + 1);

            var candidateIds = await context.Set<Meeting>()
                .Where(meeting => meeting.Status == MeetingStatuses.Scheduled
                    && meeting.ReminderOffsetMinutes.HasValue
                    && meeting.StartTime > now
                    && meeting.StartTime <= windowEnd)
                .Select(meeting => meeting.Id)
                .ToListAsync(cancellationToken);
            var pendingIds = await context.Set<MeetingReminderDispatch>()
                .Where(dispatch => dispatch.Status == MeetingReminderDispatchStatuses.Pending)
                .Select(dispatch => dispatch.MeetingId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var meetingIds = candidateIds.Concat(pendingIds).Distinct().ToArray();
            if (meetingIds.Length == 0) return;

            var meetings = await context.Set<Meeting>()
                .Where(meeting => meetingIds.Contains(meeting.Id))
                .ToListAsync(cancellationToken);
            foreach (var meeting in meetings) await ReconcileAsync(context, meeting, now, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Meeting reminder scheduling run failed");
        }
    }

    private static async Task ReconcileAsync(AppDbContext context, Meeting meeting, DateTime now, CancellationToken cancellationToken)
    {
        var pending = await context.Set<MeetingReminderDispatch>()
            .Where(dispatch => dispatch.MeetingId == meeting.Id && dispatch.Status == MeetingReminderDispatchStatuses.Pending)
            .OrderBy(dispatch => dispatch.CreatedAt)
            .ThenBy(dispatch => dispatch.Id)
            .ToListAsync(cancellationToken);

        var shouldSchedule = meeting.Status == MeetingStatuses.Scheduled
            && MeetingReminderOffsets.IsSupported(meeting.ReminderOffsetMinutes)
            && meeting.ReminderOffsetMinutes.HasValue
            && meeting.StartTime > now;
        if (!shouldSchedule)
        {
            Deactivate(pending);
            return;
        }

        var scheduledAtUtc = meeting.StartTime.AddMinutes(-meeting.ReminderOffsetMinutes!.Value);
        if (scheduledAtUtc > now)
        {
            Deactivate(pending);
            return;
        }

        var matching = pending.FirstOrDefault(dispatch => dispatch.ScheduledAtUtc == scheduledAtUtc);
        foreach (var dispatch in pending.Where(dispatch => dispatch != matching)) dispatch.IsDeleted = true;
        if (matching is null)
        {
            context.Set<MeetingReminderDispatch>().Add(new MeetingReminderDispatch
            {
                MeetingId = meeting.Id,
                TenantId = meeting.TenantId,
                ScheduledAtUtc = scheduledAtUtc,
                Status = MeetingReminderDispatchStatuses.Pending,
                CreatedBy = "System"
            });
        }
    }

    private static void Deactivate(IEnumerable<MeetingReminderDispatch> dispatches)
    {
        foreach (var dispatch in dispatches) dispatch.IsDeleted = true;
    }
}
