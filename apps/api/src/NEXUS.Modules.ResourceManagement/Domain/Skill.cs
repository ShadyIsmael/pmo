using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Domain;

public class Skill : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
