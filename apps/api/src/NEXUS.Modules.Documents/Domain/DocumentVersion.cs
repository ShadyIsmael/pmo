using NEXUS.SharedKernel;

namespace NEXUS.Modules.Documents.Domain;

public class DocumentVersion : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public int VersionNumber { get; set; }
    public string? FileName { get; set; }
    public long FileSize { get; set; }
    public string? StoragePath { get; set; }
    public string? ChangeNotes { get; set; }
    public Guid? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
