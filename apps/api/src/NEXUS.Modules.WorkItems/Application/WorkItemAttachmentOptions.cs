namespace NEXUS.Modules.WorkItems.Application;

public sealed class WorkItemAttachmentOptions
{
    public long MaximumFileSizeBytes { get; set; } = 10L * 1024L * 1024L;
    public string[] AllowedContentTypes { get; set; } = Array.Empty<string>();
}
