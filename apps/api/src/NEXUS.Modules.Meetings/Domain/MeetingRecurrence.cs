using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NEXUS.Modules.Meetings.Domain;

public static class MeetingRecurrenceFrequencies
{
    public const string Daily = "Daily";
    public const string EveryTwoDays = "EveryTwoDays";
    public const string Weekly = "Weekly";
    public const string Monthly = "Monthly";
}

public sealed class MeetingRecurrenceRule
{
    public string Frequency { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public TimeOnly LocalStartTime { get; init; }
    public int DurationMinutes { get; init; }
    public string TimeZoneId { get; init; } = string.Empty;
    public IReadOnlyList<DayOfWeek> Weekdays { get; init; } = Array.Empty<DayOfWeek>();
}

/// <summary>
/// Versioned metadata stored in the pre-existing Meetings.Recurrence column. Each generated
/// occurrence remains an independent Meeting row; the first row is the series anchor.
/// </summary>
public sealed class MeetingRecurrenceMarker
{
    public Guid SeriesId { get; init; }
    public string OccurrenceKey { get; init; } = string.Empty;
    public bool IsSeriesAnchor { get; init; }
    public MeetingRecurrenceRule? Rule { get; init; }
    public Guid? RequestId { get; init; }
    public string? RequestFingerprint { get; init; }
}

public static class MeetingRecurrenceMarkerCodec
{
    public const string Prefix = "FOPX-RECURRENCE-V1:";
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Encode(MeetingRecurrenceMarker marker) => Prefix + JsonSerializer.Serialize(marker, Options);

    public static bool TryDecode(string? value, out MeetingRecurrenceMarker? marker)
    {
        marker = null;
        if (value is null || !value.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        try
        {
            marker = JsonSerializer.Deserialize<MeetingRecurrenceMarker>(value[Prefix.Length..], Options);
            return marker is { SeriesId: var id } && id != Guid.Empty && !string.IsNullOrWhiteSpace(marker.OccurrenceKey);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static Guid DeriveSeriesId(Guid tenantId, Guid requestId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"meeting-series|{tenantId:D}|{requestId:D}"));
        var id = new Guid(hash.AsSpan(0, 16));
        return id == Guid.Empty ? new Guid(hash.AsSpan(16, 16)) : id;
    }

    public static Guid DeriveOccurrenceId(Guid seriesId, string occurrenceKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"meeting-occurrence|{seriesId:D}|{occurrenceKey}"));
        var id = new Guid(hash.AsSpan(0, 16));
        return id == Guid.Empty ? new Guid(hash.AsSpan(16, 16)) : id;
    }
}
