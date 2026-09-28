using NEXUS.Modules.Meetings.Application.DTOs;
using NEXUS.SharedKernel;
using Microsoft.AspNetCore.Http;

namespace NEXUS.Modules.Meetings.Application;

public interface IMeetingService
{
    Task<Result<MeetingDetailDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<MeetingListDto>> GetPagedAsync(int page, int pageSize, string? search = null, string? status = null, Guid? projectId = null, DateTime? dateFrom = null, DateTime? dateTo = null, CancellationToken cancellationToken = default);
    Task<ProjectMeetingOverviewDto> GetProjectOverviewAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<MeetingDetailDto>> CreateAsync(CreateMeetingRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<MeetingDetailDto>> UpdateAsync(Guid id, UpdateMeetingRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MeetingAttendeeDto>>> GetAttendeesAsync(Guid meetingId, CancellationToken cancellationToken = default);
    Task<Result<MeetingAttendeeDto>> AddAttendeeAsync(Guid meetingId, MeetingAttendeeInput request, string userId, CancellationToken cancellationToken = default);
    Task<Result> RemoveAttendeeAsync(Guid meetingId, Guid participantId, string userId, CancellationToken cancellationToken = default);
    Task<Result<MeetingMinutesDto?>> GetMinutesAsync(Guid meetingId, CancellationToken cancellationToken = default);
    Task<Result<MeetingMinutesDto>> CreateMinutesAsync(Guid meetingId, CreateMeetingMinutesRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<MeetingMinutesDto>> UpdateMinutesAsync(Guid meetingId, UpdateMeetingMinutesRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<MeetingMinutesDto>> FinalizeMinutesAsync(Guid meetingId, FinalizeMeetingMinutesRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MeetingMinutesCommentDto>>> GetCommentsAsync(Guid meetingId, CancellationToken cancellationToken = default);
    Task<Result<MeetingMinutesCommentDto>> AddCommentAsync(Guid meetingId, CreateMeetingMinutesCommentRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MeetingMinutesChecklistItemDto>>> GetChecklistAsync(Guid meetingId, CancellationToken cancellationToken = default);
    Task<Result<MeetingMinutesChecklistItemDto>> CreateChecklistItemAsync(Guid meetingId, CreateMeetingMinutesChecklistItemRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<MeetingMinutesChecklistItemDto>> UpdateChecklistItemAsync(Guid meetingId, Guid itemId, UpdateMeetingMinutesChecklistItemRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteChecklistItemAsync(Guid meetingId, Guid itemId, string userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MeetingMinutesChecklistItemDto>>> ReorderChecklistAsync(Guid meetingId, ReorderMeetingMinutesChecklistRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MeetingMinutesAttachmentDto>>> GetAttachmentsAsync(Guid meetingId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MeetingMinutesAttachmentDto>>> AddAttachmentsAsync(Guid meetingId, IReadOnlyList<IFormFile> files, string userId, CancellationToken cancellationToken = default);
    Task<Result<MeetingMinutesAttachmentContent>> GetAttachmentContentAsync(Guid meetingId, Guid attachmentId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAttachmentAsync(Guid meetingId, Guid attachmentId, string userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MeetingWorkItemLinkDto>>> GetWorkItemLinksAsync(Guid meetingId, CancellationToken cancellationToken = default);
    Task<Result<MeetingWorkItemLinkDto>> AddWorkItemLinkAsync(Guid meetingId, CreateMeetingWorkItemLinkRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result> RemoveWorkItemLinkAsync(Guid meetingId, Guid workItemId, string userId, CancellationToken cancellationToken = default);
}
