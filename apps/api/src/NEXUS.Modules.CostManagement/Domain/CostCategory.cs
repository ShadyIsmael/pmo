using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

public class CostCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool IsBillable { get; set; }
    public int SortOrder { get; set; }
}
