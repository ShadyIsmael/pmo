using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public class WorkBreakdownStructure : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid? ParentId { get; set; }
    public WorkBreakdownStructure? Parent { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? Deliverable { get; set; }
    public decimal? PlannedHours { get; set; }
    public decimal? ActualHours { get; set; }
    public decimal? Progress { get; set; }
    public int SortOrder { get; set; }
    public string? Status { get; set; }
    public ICollection<WorkBreakdownStructure> Children { get; set; } = new List<WorkBreakdownStructure>();
}
