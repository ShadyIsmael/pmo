using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public class Deliverable : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Status { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
    public Guid? OwnerId { get; set; }
    public string? QualityCriteria { get; set; }
}
