using NEXUS.Modules.Meetings.Domain;

namespace NEXUS.Modules.Meetings.Application.DTOs;

public sealed record MeetingRecurrenceOccurrencePreview(
    string OccurrenceKey,
    DateTime StartTime,
    DateTime EndTime,
    string TimeZoneId);

public sealed record MeetingRecurrencePreviewDto(
    IReadOnlyList<MeetingRecurrenceOccurrencePreview> Occurrences,
    int TotalOccurrenceCount,
    int SkippedDaylightSavingCount,
    string TimeZoneId);

public sealed record MeetingSeriesOccurrenceDto(
    Guid Id,
    string OccurrenceKey,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    string? Status,
    bool IsSeriesAnchor);

public sealed record MeetingSeriesDto(
    Guid Id,
    MeetingRecurrenceRule Rule,
    string TemplateTitle,
    IReadOnlyList<MeetingSeriesOccurrenceDto> Occurrences);

public sealed class CreateMeetingSeriesRequest
{
    public Guid RequestId { get; init; }
    public CreateMeetingRequest Template { get; init; } = new();
    public MeetingRecurrenceRule Rule { get; init; } = new();
}

public sealed class UpdateMeetingSeriesRequest
{
    public string Scope { get; init; } = "EntireSeries";
    public CreateMeetingRequest Template { get; init; } = new();
    public MeetingRecurrenceRule Rule { get; init; } = new();
    public Guid TargetOccurrenceId { get; init; }
}

public static class MeetingRecurrenceScopes
{
    public const string ThisOccurrence = "ThisOccurrence";
    public const string ThisAndFollowing = "ThisAndFollowing";
    public const string EntireSeries = "EntireSeries";
}
