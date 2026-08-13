using NEXUS.SharedKernel;

namespace NEXUS.Modules.IssueManagement.Domain;

public class IssueCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public int SortOrder { get; set; }
}
