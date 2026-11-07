namespace NEXUS.Modules.Meetings.Application;

/// <summary>
/// Restricts meeting links to absolute HTTP(S) URLs so a stored value can never be rendered as a
/// <c>javascript:</c> or <c>data:</c> navigation target.
/// </summary>
public static class MeetingLinkPolicy
{
    public static bool TryNormalize(string? value, out string? meetingLink)
    {
        meetingLink = null;
        if (string.IsNullOrWhiteSpace(value)) return true;

        var trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

        meetingLink = trimmed;
        return true;
    }

    /// <summary>
    /// Returns the trimmed link when it is an allowed absolute URL, otherwise <c>null</c>.
    /// Call <see cref="TryNormalize"/> first to reject invalid input explicitly.
    /// </summary>
    public static string? Normalize(string? value)
        => TryNormalize(value, out var meetingLink) ? meetingLink : null;
}
