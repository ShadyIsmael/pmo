using NEXUS.SharedKernel;

namespace NEXUS.Modules.Documents.Domain;

public class DocumentCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
}
