using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Meetings.Domain;

namespace NEXUS.Persistence;

/// <summary>Schedules meeting reminders and queues them through the shared notification pipeline.</summary>
public sealed class MeetingReminderScheduler : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private const int MaximumReminderOffsetMinutes = MeetingReminderOffsets.OneDay;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MeetingReminderScheduler> _logger;
    private readonly TimeProvider _timeProvider;

    public MeetingReminderScheduler(
        IServiceScopeFactory scopeFactory,
        ILogger<MeetingReminderScheduler> logger,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
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
            var notificationPublisher = scope.ServiceProvider.GetRequiredService<IMeetingNotificationPublisher>();
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var windowEnd = now.AddMinutes(MaximumReminderOffsetMinutes + 1);

            // Hosted services have no request tenant. Process each tenant explicitly and
            // retain tenant ids on every dispatch passed to the notification publisher.
            var candidateIds = await context.Set<Meeting>()
                .IgnoreQueryFilters()
                .Where(meeting => !meeting.IsDeleted
                    && meeting.Status == MeetingStatuses.Scheduled
                    && meeting.ReminderOffsetMinutes.HasValue
                    && meeting.StartTime > now
                    && meeting.StartTime <= windowEnd)
                .Select(meeting => meeting.Id)
                .ToListAsync(cancellationToken);
            var pendingIds = await context.Set<MeetingReminderDispatch>()
                .IgnoreQueryFilters()
                .Where(dispatch => !dispatch.IsDeleted && dispatch.Status == MeetingReminderDispatchStatuses.Pending)
                .Select(dispatch => dispatch.MeetingId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var meetingIds = candidateIds.Concat(pendingIds).Distinct().ToArray();
            if (meetingIds.Length == 0) return;

            var meetings = await context.Set<Meeting>()
                .IgnoreQueryFilters()
                .Where(meeting => !meeting.IsDeleted && meetingIds.Contains(meeting.Id))
                .ToListAsync(cancellationToken);
            var activeMeetingIds = meetings.Select(meeting => meeting.Id).ToHashSet();
            var orphanedDispatches = await context.Set<MeetingReminderDispatch>()
                .IgnoreQueryFilters()
                .Where(dispatch => !dispatch.IsDeleted
                    && dispatch.Status == MeetingReminderDispatchStatuses.Pending
                    && meetingIds.Contains(dispatch.MeetingId)
                    && !activeMeetingIds.Contains(dispatch.MeetingId))
                .ToListAsync(cancellationToken);
            foreach (var dispatch in orphanedDispatches) dispatch.IsDeleted = true;
            foreach (var meeting in meetings) await ReconcileAsync(context, meeting, now, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            var dueDispatches = await context.Set<MeetingReminderDispatch>()
                .IgnoreQueryFilters()
                .Where(dispatch => !dispatch.IsDeleted
                    && dispatch.Status == MeetingReminderDispatchStatuses.Pending
                    && dispatch.ScheduledAtUtc <= now)
                .OrderBy(dispatch => dispatch.ScheduledAtUtc)
                .ThenBy(dispatch => dispatch.Id)
                .ToListAsync(cancellationToken);
            foreach (var dispatch in dueDispatches)
                await DeliverAsync(context, notificationPublisher, dispatch, _timeProvider, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Meeting reminder scheduling or delivery run failed");
        }
    }

    private static async Task ReconcileAsync(AppDbContext context, Meeting meeting, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var existing = await context.Set<MeetingReminderDispatch>()
            .IgnoreQueryFilters()
            .Where(dispatch => !dispatch.IsDeleted && dispatch.MeetingId == meeting.Id)
            .OrderBy(dispatch => dispatch.CreatedAt)
            .ThenBy(dispatch => dispatch.Id)
            .ToListAsync(cancellationToken);
        var pending = existing.Where(dispatch => dispatch.Status == MeetingReminderDispatchStatuses.Pending).ToArray();

        var shouldSchedule = meeting.Status == MeetingStatuses.Scheduled
            && MeetingReminderOffsets.IsSupported(meeting.ReminderOffsetMinutes)
            && meeting.ReminderOffsetMinutes.HasValue
            && Utc(meeting.StartTime) > nowUtc;
        if (!shouldSchedule)
        {
            Deactivate(pending);
            return;
        }

        var scheduledAtUtc = Utc(meeting.StartTime).AddMinutes(-meeting.ReminderOffsetMinutes!.Value);
        if (scheduledAtUtc > nowUtc)
        {
            Deactivate(pending);
            return;
        }

        var matching = pending.FirstOrDefault(dispatch => Utc(dispatch.ScheduledAtUtc) == scheduledAtUtc);
        foreach (var dispatch in pending.Where(dispatch => dispatch != matching)) dispatch.IsDeleted = true;
        if (matching is not null) return;

        // A completed dispatch for this occurrence/time must not be recreated on later polls.
        if (existing.Any(dispatch => Utc(dispatch.ScheduledAtUtc) == scheduledAtUtc
                                     && dispatch.Status != MeetingReminderDispatchStatuses.Pending)) return;

        context.Set<MeetingReminderDispatch>().Add(new MeetingReminderDispatch
        {
            MeetingId = meeting.Id,
            TenantId = meeting.TenantId,
            ScheduledAtUtc = scheduledAtUtc,
            Status = MeetingReminderDispatchStatuses.Pending,
            CreatedBy = "System"
        });
    }

    private static async Task DeliverAsync(
        AppDbContext context,
        IMeetingNotificationPublisher notificationPublisher,
        MeetingReminderDispatch dispatch,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        // Revalidate under a serializable transaction so Start/Cancel/reschedule cannot race
        // the final dispatch decision. The provider holds the Meeting read lock until commit.
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var currentDispatch = await context.Set<MeetingReminderDispatch>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == dispatch.Id
                                             && item.TenantId == dispatch.TenantId
                                             && !item.IsDeleted,
                    cancellationToken);
            if (currentDispatch is null
                || currentDispatch.Status != MeetingReminderDispatchStatuses.Pending
                || Utc(currentDispatch.ScheduledAtUtc) != Utc(dispatch.ScheduledAtUtc))
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var meeting = await context.Set<Meeting>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == currentDispatch.MeetingId
                                             && item.TenantId == currentDispatch.TenantId
                                             && !item.IsDeleted,
                    cancellationToken);
            var deliveryNowUtc = timeProvider.GetUtcNow().UtcDateTime;
            var scheduledAtUtc = Utc(currentDispatch.ScheduledAtUtc);
            if (meeting is null
                || meeting.Status != MeetingStatuses.Scheduled
                || !meeting.ReminderOffsetMinutes.HasValue
                || !MeetingReminderOffsets.IsSupported(meeting.ReminderOffsetMinutes)
                || Utc(meeting.StartTime) <= deliveryNowUtc
                || scheduledAtUtc != Utc(meeting.StartTime).AddMinutes(-meeting.ReminderOffsetMinutes.Value)
                || scheduledAtUtc > deliveryNowUtc)
            {
                dispatch.IsDeleted = true;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            // Re-read invitees and accounts within the same transaction so invitation changes
            // made after dispatch creation are honored by the final queue decision.
            var recipients = await context.Set<MeetingParticipant>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(item => item.MeetingId == meeting.Id && item.TenantId == meeting.TenantId
                               && !item.IsDeleted && item.UserId.HasValue)
                .ToListAsync(cancellationToken);
            recipients = recipients
                .Where(IsCurrentInvite)
                .GroupBy(item => item.UserId!.Value)
                .Select(group => group.OrderBy(item => item.Id).First())
                .ToList();

            var recipientIds = recipients.Select(item => item.UserId!.Value).ToArray();
            var activeUsers = await context.Set<User>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user => recipientIds.Contains(user.Id)
                    && user.TenantId == meeting.TenantId
                    && !user.IsDeleted
                    && user.IsActive)
                .Select(user => user.Id)
                .ToHashSetAsync(cancellationToken);
            recipients = recipients.Where(item => activeUsers.Contains(item.UserId!.Value)).ToList();

            if (recipients.Count == 0)
            {
                dispatch.Status = MeetingReminderDispatchStatuses.NoRecipients;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            // The dispatch transition and every reminder intent commit together. RowVersion
            // and the publisher's stable deduplication identity protect retries and workers.
            dispatch.Status = MeetingReminderDispatchStatuses.Delivered;
            foreach (var recipient in recipients)
            {
                var result = await notificationPublisher.QueueReminderAsync(meeting, recipient, scheduledAtUtc, cancellationToken);
                if (result.IsFailure) throw new InvalidOperationException("A meeting reminder could not be queued.");
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static bool IsCurrentInvite(MeetingParticipant participant)
    {
        var response = MeetingInvitationResponseStatuses.Canonicalize(participant.InvitationResponseStatus);
        if (string.IsNullOrWhiteSpace(participant.InvitationResponseStatus)) return true;
        return response is MeetingInvitationResponseStatuses.Pending
            or MeetingInvitationResponseStatuses.Accepted
            or MeetingInvitationResponseStatuses.Tentative;
    }

    private static void Deactivate(IEnumerable<MeetingReminderDispatch> dispatches)
    {
        foreach (var dispatch in dispatches) dispatch.IsDeleted = true;
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
