namespace NEXUS.Modules.Meetings.Domain;

public static class MeetingActionStatuses
{
    public const string Pending = "Pending";
    public const string Open = Pending;
    public const string Completed = "Completed";

    public static string? Canonicalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "pending" or "open" or "inprogress" or "in progress" => Pending,
        "completed" => Completed,
        _ => null
    };
}
