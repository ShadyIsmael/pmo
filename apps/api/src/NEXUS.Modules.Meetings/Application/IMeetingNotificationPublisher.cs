using NEXUS.Modules.Meetings.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Application;

/// <summary>Queues meeting notifications through the shared notification and delivery pipeline.</summary>
public interface IMeetingNotificationPublisher
{
    Task<Result> QueueInvitationAsync(Meeting meeting, MeetingParticipant invitee, Guid actorId, CancellationToken cancellationToken = default);
    Task<Result> QueueReminderAsync(Meeting meeting, MeetingParticipant recipient, DateTime scheduledAtUtc, CancellationToken cancellationToken = default);
    Task<Result> QueueStartedAsync(Meeting meeting, Guid actorId, CancellationToken cancellationToken = default);
    Task<Result> QueueMinutesPublishedAsync(Meeting meeting, MeetingMinutes minutes, Guid actorId, CancellationToken cancellationToken = default);
    Task<Result> QueueMinutesCommentAsync(Meeting meeting, MeetingMinutes minutes, MeetingMinutesComment comment, Guid actorId, CancellationToken cancellationToken = default);
}
