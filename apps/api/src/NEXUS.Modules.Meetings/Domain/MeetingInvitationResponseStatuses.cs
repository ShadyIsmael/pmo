namespace NEXUS.Modules.Meetings.Domain;

public static class MeetingInvitationResponseStatuses
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Declined = "Declined";
    public const string Tentative = "Tentative";

    public static bool IsSupported(string? value) => Canonicalize(value) is not null;

    public static string? Canonicalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "pending" => Pending,
        "accepted" => Accepted,
        "declined" => Declined,
        "tentative" => Tentative,
        _ => null
    };
}
