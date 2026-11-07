using NEXUS.Modules.Meetings.Domain;

namespace NEXUS.Modules.Meetings.Application.DTOs;

public sealed class MeetingListDto
{
    public Guid Id { get; init; }
    public Guid PublicId { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid? ProjectId { get; init; }
    public Guid? ProjectPublicId { get; init; }
    public string? ProjectName { get; init; }
    public string? ProjectCode { get; init; }
    public Guid? OrganizerId { get; init; }
    public string? OrganizerName { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public DateTime? ActualStartTime { get; init; }
    public DateTime? ActualEndTime { get; init; }
    public string? Status { get; init; }
    public int? ReminderOffsetMinutes { get; init; }
    public int AttendeeCount { get; init; }
    public string? MinutesState { get; init; }
    public bool IsRecurring { get; init; }
    public Guid? SeriesId { get; init; }
    public string? OccurrenceKey { get; init; }
}

public sealed class MeetingCalendarDto
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public int TotalCount { get; init; }
    public bool IsTruncated { get; init; }
    public IReadOnlyList<MeetingListDto> Items { get; init; } = Array.Empty<MeetingListDto>();
}

public sealed class MeetingProjectOptionDto
{
    public Guid Id { get; init; }
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
}

public sealed class MeetingDetailDto
{
    public Guid Id { get; init; }
    public Guid PublicId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Objective { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ProjectPublicId { get; init; }
    public string? ProjectName { get; init; }
    public Guid? OrganizerId { get; init; }
    public string? OrganizerName { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public DateTime? ActualStartTime { get; init; }
    public DateTime? ActualEndTime { get; init; }
    public string? Location { get; init; }
    public string? MeetingLink { get; init; }
    public string? Status { get; init; }
    public int? ReminderOffsetMinutes { get; init; }
    public IReadOnlyList<MeetingAttendeeDto> Attendees { get; init; } = Array.Empty<MeetingAttendeeDto>();
    public string? MinutesState { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
    public bool IsRecurring { get; init; }
    public Guid? SeriesId { get; init; }
    public string? OccurrenceKey { get; init; }
}

public class CreateMeetingRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Objective { get; init; }
    public Guid? ProjectId { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public string? Location { get; init; }
    public string? MeetingLink { get; init; }
    public Guid? OrganizerId { get; init; }
    public IReadOnlyList<MeetingAttendeeInput> Attendees { get; init; } = Array.Empty<MeetingAttendeeInput>();
    public IReadOnlyList<CreateMeetingAgendaItemRequest> AgendaItems { get; init; } = Array.Empty<CreateMeetingAgendaItemRequest>();
    public string? Status { get; init; }
    public int? ReminderOffsetMinutes { get; init; }
}

public sealed class UpdateMeetingRequest : CreateMeetingRequest
{
    public byte[]? RowVersion { get; init; }
}

public sealed class MeetingAttendeeDto
{
    public Guid Id { get; init; }
    public Guid MeetingId { get; init; }
    public Guid? UserId { get; init; }
    public string? DisplayName { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool IsInternalUser { get; init; }
}

public sealed class MeetingAttendeeInput
{
    public Guid? UserId { get; init; }
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
}

public sealed class MeetingMinutesDto
{
    public Guid Id { get; init; }
    public Guid MeetingId { get; init; }
    public string? Summary { get; init; }
    public string? DiscussionNotes { get; init; }
    public string? Decisions { get; init; }
    public string? Outcomes { get; init; }
    public string State { get; init; } = MeetingMinutesStates.Draft;
    public Guid? RecordedBy { get; init; }
    public DateTime RecordedAt { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public class CreateMeetingMinutesRequest
{
    public string? Summary { get; init; }
    public string? DiscussionNotes { get; init; }
    public string? Decisions { get; init; }
    public string? Outcomes { get; init; }
}

public sealed class UpdateMeetingMinutesRequest : CreateMeetingMinutesRequest
{
    public byte[]? RowVersion { get; init; }
}

public sealed class FinalizeMeetingMinutesRequest
{
    public byte[]? RowVersion { get; init; }
}

public sealed class MeetingMinutesCommentDto
{
    public Guid Id { get; init; }
    public Guid PublicId { get; init; }
    public Guid MeetingMinutesId { get; init; }
    public Guid? ParentCommentId { get; init; }
    public string CommentText { get; init; } = string.Empty;
    public string CreatedBy { get; init; } = string.Empty;
    public string? AuthorName { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class CreateMeetingMinutesCommentRequest
{
    public string CommentText { get; init; } = string.Empty;
    public Guid? ParentCommentId { get; init; }
}

public sealed class MeetingMinutesChecklistItemDto
{
    public Guid Id { get; init; }
    public Guid MeetingMinutesId { get; init; }
    public string Title { get; init; } = string.Empty;
    public bool IsCompleted { get; init; }
    public int SortOrder { get; init; }
}

public sealed class CreateMeetingMinutesChecklistItemRequest
{
    public string Title { get; init; } = string.Empty;
}

public sealed class UpdateMeetingMinutesChecklistItemRequest
{
    public string Title { get; init; } = string.Empty;
    public bool IsCompleted { get; init; }
}

public sealed class ReorderMeetingMinutesChecklistRequest
{
    public IReadOnlyList<Guid> ItemIds { get; init; } = Array.Empty<Guid>();
}

public sealed class MeetingMinutesAttachmentDto
{
    public Guid Id { get; init; }
    public Guid MeetingMinutesId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class MeetingMinutesAttachmentContent
{
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
}

public sealed class MeetingWorkItemLinkDto
{
    public Guid Id { get; init; }
    public Guid MeetingMinutesId { get; init; }
    public Guid WorkItemId { get; init; }
    public string? WorkItemTitle { get; init; }
    public string? WorkItemType { get; init; }
    public string? WorkItemStatus { get; init; }
    public Guid? ProjectId { get; init; }
}

public sealed class CreateMeetingWorkItemLinkRequest
{
    public Guid WorkItemId { get; init; }
}
