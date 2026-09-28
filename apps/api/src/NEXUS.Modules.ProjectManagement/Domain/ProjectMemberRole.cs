using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public class ProjectMemberRole : BaseEntity
{
    public Guid ProjectMemberId { get; set; }
    public ProjectMember ProjectMember { get; set; } = null!;
    public Guid ProjectRoleId { get; set; }
    public bool IsPrimary { get; set; }
}
