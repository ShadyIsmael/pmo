using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Api.Tests.Testing;

internal sealed class NoOpMeetingNotificationPublisher : IMeetingNotificationPublisher
{
    public Task<Result> QueueInvitationAsync(Meeting meeting, MeetingParticipant invitee, Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
    public Task<Result> QueueReminderAsync(Meeting meeting, MeetingParticipant recipient, DateTime scheduledAtUtc, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
    public Task<Result> QueueStartedAsync(Meeting meeting, Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
    public Task<Result> QueueMinutesPublishedAsync(Meeting meeting, MeetingMinutes minutes, Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
    public Task<Result> QueueMinutesCommentAsync(Meeting meeting, MeetingMinutes minutes, MeetingMinutesComment comment, Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult(Result.Success());
}
