using NEXUS.Modules.Meetings.Domain;

namespace NEXUS.Modules.Meetings.Application.DTOs;

public sealed class MeetingAgendaItemDto
{
    public Guid Id { get; init; }
    public Guid MeetingId { get; init; }
    public string Topic { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int SortOrder { get; init; }
    public int? DurationMinutes { get; init; }
    public Guid? OwnerId { get; init; }
    public string? OwnerName { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public class CreateMeetingAgendaItemRequest
{
    public string Topic { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int? DurationMinutes { get; init; }
    public Guid? OwnerId { get; init; }
}

public sealed class UpdateMeetingAgendaItemRequest : CreateMeetingAgendaItemRequest
{
    public byte[]? RowVersion { get; init; }
}

public sealed class ReorderMeetingAgendaRequest
{
    public IReadOnlyList<Guid> ItemIds { get; init; } = Array.Empty<Guid>();
}

public sealed class MeetingParticipantStatusDto
{
    public Guid Id { get; init; }
    public Guid MeetingId { get; init; }
    public Guid? UserId { get; init; }
    public string? DisplayName { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool IsInternalUser { get; init; }
    // AttendanceStatus remains as a compatibility projection for older clients.
    public string AttendanceStatus { get; init; } = MeetingInvitationResponseStatuses.Pending;
    public string? InvitationResponseStatus { get; init; }
    public string? ActualAttendanceStatus { get; init; }
    public string? Role { get; init; }
    public DateTime? RespondedAt { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public sealed class UpdateMeetingAttendanceRequest
{
    public string AttendanceStatus { get; init; } = string.Empty;
    public string? ActualAttendanceStatus { get; init; }
    public byte[]? RowVersion { get; init; }
}

public sealed class MeetingDecisionDto
{
    public string? Decisions { get; init; }
    public bool IsEditable { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public sealed class UpdateMeetingDecisionsRequest
{
    public string? Decisions { get; init; }
    public byte[]? RowVersion { get; init; }
}

public sealed class MeetingActionItemDto
{
    public Guid Id { get; init; }
    public Guid MeetingId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? AssigneeId { get; init; }
    public string? AssigneeName { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? CompletedDate { get; init; }
    public string Status { get; init; } = MeetingActionStatuses.Pending;
    public Guid? WorkItemId { get; init; }
    public string? WorkItemTitle { get; init; }
    public Guid? AgendaItemId { get; init; }
    public string? AgendaItemTopic { get; init; }
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public sealed class LinkMeetingActionWorkItemRequest
{
    public Guid WorkItemId { get; init; }
}

public class CreateMeetingActionItemRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? AssigneeId { get; init; }
    public DateTime? DueDate { get; init; }
    public Guid? AgendaItemId { get; init; }
}

public sealed class UpdateMeetingActionItemRequest : CreateMeetingActionItemRequest
{
    public string Status { get; init; } = MeetingActionStatuses.Open;
    public byte[]? RowVersion { get; init; }
}

public sealed class MeetingLifecycleRequest
{
    public byte[]? RowVersion { get; init; }
}
