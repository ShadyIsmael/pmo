using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public class ProjectManagerAssignment : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid ProjectManagerId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public Guid? HandoverFromProjectManagerId { get; set; }
    public DateTime? HandoverDate { get; set; }
    public string? HandoverReason { get; set; }
    public string? HandoverNotes { get; set; }
}
