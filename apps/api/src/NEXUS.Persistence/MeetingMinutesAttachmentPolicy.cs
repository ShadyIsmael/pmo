using Microsoft.Extensions.Options;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.WorkItems.Application;

namespace NEXUS.Persistence;

public sealed class MeetingMinutesAttachmentPolicy : IMeetingMinutesAttachmentPolicy
{
    public MeetingMinutesAttachmentPolicy(IOptions<WorkItemAttachmentOptions> options)
    {
        MaximumFileSizeBytes = options.Value.MaximumFileSizeBytes;
        AllowedContentTypes = (options.Value.AllowedContentTypes ?? Array.Empty<string>())
            .Where(contentType => !string.IsNullOrWhiteSpace(contentType))
            .Select(contentType => contentType.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public long MaximumFileSizeBytes { get; }
    public IReadOnlySet<string> AllowedContentTypes { get; }
}
