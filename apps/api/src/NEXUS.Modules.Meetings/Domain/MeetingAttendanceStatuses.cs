namespace NEXUS.Modules.Meetings.Domain;

public static class MeetingAttendanceStatuses
{
    public const string Present = "Present";
    public const string Late = "Late";
    public const string Excused = "Excused";
    public const string Absent = "Absent";

    public static bool IsSupported(string? value) => Canonicalize(value) is not null;

    public static string? Canonicalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "present" or "attended" => Present,
        "late" => Late,
        "excused" => Excused,
        "absent" => Absent,
        _ => null
    };
}
