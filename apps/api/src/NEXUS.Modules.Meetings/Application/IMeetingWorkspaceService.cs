using NEXUS.Modules.Meetings.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Application;

public interface IMeetingWorkspaceService
{
    Task<Result<IReadOnlyList<MeetingAgendaItemDto>>> GetAgendaAsync(Guid meetingId, CancellationToken cancellationToken = default);
    Task<Result<MeetingAgendaItemDto>> CreateAgendaItemAsync(Guid meetingId, CreateMeetingAgendaItemRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<MeetingAgendaItemDto>> UpdateAgendaItemAsync(Guid meetingId, Guid itemId, UpdateMeetingAgendaItemRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAgendaItemAsync(Guid meetingId, Guid itemId, string userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MeetingAgendaItemDto>>> ReorderAgendaAsync(Guid meetingId, ReorderMeetingAgendaRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MeetingParticipantStatusDto>>> GetParticipantsAsync(Guid meetingId, CancellationToken cancellationToken = default);
    Task<Result<MeetingParticipantStatusDto>> UpdateMyAttendanceAsync(Guid meetingId, UpdateMeetingAttendanceRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<MeetingParticipantStatusDto>> UpdateAttendanceAsync(Guid meetingId, Guid participantId, UpdateMeetingAttendanceRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<MeetingParticipantStatusDto>> UpdateOrganizerAttendanceAsync(Guid meetingId, Guid participantId, UpdateMeetingAttendanceRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<MeetingDecisionDto>> GetDecisionsAsync(Guid meetingId, CancellationToken cancellationToken = default);
    Task<Result<MeetingDecisionDto>> SaveDecisionsAsync(Guid meetingId, UpdateMeetingDecisionsRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MeetingActionItemDto>>> GetActionItemsAsync(Guid meetingId, CancellationToken cancellationToken = default);
    Task<Result<MeetingActionItemDto>> CreateActionItemAsync(Guid meetingId, CreateMeetingActionItemRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<MeetingActionItemDto>> UpdateActionItemAsync(Guid meetingId, Guid itemId, UpdateMeetingActionItemRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<MeetingActionItemDto>> LinkActionWorkItemAsync(Guid meetingId, Guid itemId, LinkMeetingActionWorkItemRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result> UnlinkActionWorkItemAsync(Guid meetingId, Guid itemId, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteActionItemAsync(Guid meetingId, Guid itemId, string userId, CancellationToken cancellationToken = default);
}
