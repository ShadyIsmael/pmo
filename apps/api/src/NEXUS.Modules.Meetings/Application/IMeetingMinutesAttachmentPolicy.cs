namespace NEXUS.Modules.Meetings.Application;

public interface IMeetingMinutesAttachmentPolicy
{
    long MaximumFileSizeBytes { get; }
    IReadOnlySet<string> AllowedContentTypes { get; }
}
