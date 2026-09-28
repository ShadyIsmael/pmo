namespace NEXUS.Modules.Meetings.Domain;

public static class MeetingReminderOffsets
{
    public const int FifteenMinutes = 15;
    public const int ThirtyMinutes = 30;
    public const int OneHour = 60;
    public const int OneDay = 1440;

    public static readonly IReadOnlySet<int> Supported = new HashSet<int>
    {
        FifteenMinutes,
        ThirtyMinutes,
        OneHour,
        OneDay
    };

    public static bool IsSupported(int? value) => !value.HasValue || Supported.Contains(value.Value);
}
