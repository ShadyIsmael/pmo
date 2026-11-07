using NEXUS.Modules.Meetings.Application;
using Xunit;

namespace NEXUS.Api.Tests.Security;

/// <summary>
/// Regression coverage for the meeting-link policy: only absolute HTTP(S) URLs may be stored, so a
/// stored value can never be rendered as a <c>javascript:</c> navigation target.
/// </summary>
public class MeetingLinkPolicyTests
{
    [Theory]
    [InlineData("https://meet.example.com/room-1")]
    [InlineData("http://meet.example.com/room-1")]
    [InlineData("  https://meet.example.com/room-1  ")]
    public void TryNormalize_accepts_absolute_http_urls(string value)
    {
        Assert.True(MeetingLinkPolicy.TryNormalize(value, out var meetingLink));
        Assert.Equal(value.Trim(), meetingLink);
    }

    [Theory]
    [InlineData("javascript:alert(document.cookie)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("ftp://files.example.com/doc")]
    [InlineData("/relative/path")]
    [InlineData("meet.example.com/room-1")]
    public void TryNormalize_rejects_non_http_schemes(string value)
    {
        Assert.False(MeetingLinkPolicy.TryNormalize(value, out var meetingLink));
        Assert.Null(meetingLink);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalize_allows_an_empty_link(string? value)
    {
        Assert.True(MeetingLinkPolicy.TryNormalize(value, out var meetingLink));
        Assert.Null(meetingLink);
    }
}
