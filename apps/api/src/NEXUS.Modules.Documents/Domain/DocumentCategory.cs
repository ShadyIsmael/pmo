using NEXUS.SharedKernel;

namespace NEXUS.Modules.Documents.Domain;

public class DocumentCategory : BaseEntity
{
    /// <summary>English display name. Kept as Name for compatibility with existing category references.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Arabic display name for organization-managed categories; null on legacy tenant-wide rows.</summary>
    public string? NameAr { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
    /// <summary>Null only for legacy tenant-wide categories retained for historical references.</summary>
    public Guid? OrganizationId { get; set; }
    public string NormalizedNameEn { get; set; } = string.Empty;
    public string? NormalizedNameAr { get; set; }
    public bool IsActive { get; set; } = true;
}
