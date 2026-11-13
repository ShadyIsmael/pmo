using System.ComponentModel.DataAnnotations;
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
    /// <summary>Provider-neutral identity for the stored object; null only for legacy rows.</summary>
    [MaxLength(64)]
    public string? StorageProviderId { get; set; }
    /// <summary>Opaque key interpreted only by the configured storage provider; never a filesystem path.</summary>
    [MaxLength(512)]
    public string? StorageKey { get; set; }
    /// <summary>Lowercase SHA-256 checksum of the stored bytes; null only for legacy rows.</summary>
    [MaxLength(64)]
    public string? Sha256 { get; set; }
    public string? ContentType { get; set; }
    public string? ChangeNotes { get; set; }
    public Guid? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsCurrent { get; set; }
}
