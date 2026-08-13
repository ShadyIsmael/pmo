using NEXUS.SharedKernel;

namespace NEXUS.Modules.RiskManagement.Domain;

public class RiskCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public int SortOrder { get; set; }
}
