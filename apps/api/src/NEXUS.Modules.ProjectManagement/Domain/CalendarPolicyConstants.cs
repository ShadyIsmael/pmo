namespace NEXUS.Modules.ProjectManagement.Domain;

public static class CalendarPolicyConstants
{
    public const string UseResourceCalendar = "UseResourceCalendar";
    public const string FollowProjectCalendar = "FollowProjectCalendar";

    public static bool IsValid(string? value) => value is UseResourceCalendar or FollowProjectCalendar;
}
