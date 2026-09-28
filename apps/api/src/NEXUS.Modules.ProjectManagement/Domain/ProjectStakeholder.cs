using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public class ProjectStakeholder : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid? UserId { get; set; }
    /// <summary>Canonical Resource Management identity. Null is retained only for historical legacy contacts.</summary>
    public Guid? ResourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string Role { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = "Stakeholder";
    public string? Interest { get; set; }
    public string? Influence { get; set; }
    public ProjectStakeholderStatus Status { get; set; } = ProjectStakeholderStatus.Active;
}

public enum ProjectStakeholderStatus
{
    Active,
    ContactOnly,
    Revoked
}
