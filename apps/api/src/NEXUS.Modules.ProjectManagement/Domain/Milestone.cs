using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public class Milestone : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? Status { get; set; }
    public decimal? Progress { get; set; }
    public string? Category { get; set; }
    public Guid? AssigneeId { get; set; }
}
