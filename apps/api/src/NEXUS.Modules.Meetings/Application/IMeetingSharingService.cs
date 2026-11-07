using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Application;

public interface IMeetingSharingService
{
    Task<Result<MeetingSharingRecipientsResponse>> GetRecipientsAsync(Guid meetingId, string? search, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MeetingSharingHistoryEntryDto>>> GetHistoryAsync(Guid meetingId, CancellationToken cancellationToken = default);
    Task<Result<MeetingSharingOperationResult>> SendMinutesEmailAsync(Guid meetingId, SendMeetingMinutesEmailRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<Result<MeetingSharingOperationResult>> NotifyCompletionAsync(Guid meetingId, SendMeetingCompletedNotificationRequest request, Guid actorId, CancellationToken cancellationToken = default);
}
