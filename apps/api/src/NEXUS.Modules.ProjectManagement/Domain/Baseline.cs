using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public class Baseline : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Version { get; set; }
    public DateTime? BaselineDate { get; set; }
    public string? Data { get; set; }
    public bool IsCurrent { get; set; }
}
