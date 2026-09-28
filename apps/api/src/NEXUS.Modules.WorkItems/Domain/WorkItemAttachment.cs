using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkItems.Domain;

public sealed class WorkItemAttachment : BaseEntity
{
    public Guid WorkItemId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string StoragePath { get; set; } = string.Empty;
}
