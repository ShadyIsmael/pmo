namespace NEXUS.Modules.Meetings.Application;

public sealed record MeetingSharingRecipientDto(
    Guid UserId,
    string DisplayName,
    string Email,
    bool IsAttendeeShortcut,
    string? AttendanceStatus,
    bool CanReceiveMinutesEmail,
    bool CanReceiveCompletionNotification,
    bool MinutesEmailPreferenceEnabled,
    bool MinutesEmailProviderAvailable);

public sealed record MeetingSharingRecipientsResponse(
    IReadOnlyList<MeetingSharingRecipientDto> Attendees,
    IReadOnlyList<MeetingSharingRecipientDto> TenantUsers,
    string ApplicationLink);

public sealed record MeetingSharingHistoryEntryDto(
    Guid Id,
    string Type,
    string SenderName,
    DateTime SentAt,
    int RecipientCount,
    string Status,
    IReadOnlyList<MeetingSharingHistoryRecipientDto> Recipients);

public sealed record MeetingSharingHistoryRecipientDto(
    Guid UserId,
    string RecipientName,
    string? RecipientEmail,
    string Status,
    IReadOnlyList<MeetingSharingHistoryDeliveryDto> Deliveries);

public sealed record MeetingSharingHistoryDeliveryDto(string Channel, string Status);

public sealed record MeetingSharingOperationResult(int QueuedCount, int AlreadyProcessedCount);

public sealed class SendMeetingMinutesEmailRequest
{
    public IReadOnlyList<Guid> RecipientUserIds { get; init; } = Array.Empty<Guid>();
    public Guid MinutesId { get; init; }
    public byte[]? MinutesRowVersion { get; init; }
    public Guid RequestId { get; init; }
    public string Locale { get; init; } = "en";
}

public sealed class SendMeetingCompletedNotificationRequest
{
    public IReadOnlyList<Guid> RecipientUserIds { get; init; } = Array.Empty<Guid>();
    public Guid RequestId { get; init; }
    public string Locale { get; init; } = "en";
}

public sealed class MeetingSharingOptions
{
    public const string SectionName = "Meetings:Sharing";
    public string ApplicationBaseUrl { get; set; } = string.Empty;
}
