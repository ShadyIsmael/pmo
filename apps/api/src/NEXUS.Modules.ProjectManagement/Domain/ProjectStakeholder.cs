using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public class ProjectStakeholder : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? Interest { get; set; }
    public string? Influence { get; set; }
}
