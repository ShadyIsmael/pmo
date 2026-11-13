using NEXUS.SharedKernel;

namespace NEXUS.Modules.Documents.Domain;

public class Document : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FileName { get; set; }
    public string? FileType { get; set; }
    public long? FileSize { get; set; }
    public string? StoragePath { get; set; }
    public string? ContentType { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? PortfolioId { get; set; }
    public Guid? FolderId { get; set; }
    public string? Tags { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public Guid? OwnerId { get; set; }
    public Guid? CategoryId { get; set; }
    /// <summary>Owner organization for organization-approved templates. Null on legacy and project documents.</summary>
    public Guid? OrganizationId { get; set; }
    public bool IsTemplate { get; set; }
    /// <summary>Template-level default designation. Does not drive Phase Gate requirements.</summary>
    public bool IsMandatory { get; set; }
    public bool IsArchived { get; set; }
    public Guid? SourceTemplateId { get; set; }
    public int? SourceTemplateVersionNumber { get; set; }
    public Guid? SourceTemplateVersionId { get; set; }
    public DocumentVersion? SourceTemplateVersion { get; set; }
    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
    public ICollection<DocumentReview> Reviews { get; set; } = new List<DocumentReview>();
}
