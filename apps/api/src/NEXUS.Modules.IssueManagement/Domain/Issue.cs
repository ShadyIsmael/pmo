using NEXUS.SharedKernel;

namespace NEXUS.Modules.IssueManagement.Domain;

public class Issue : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? CategoryId { get; set; }
    public IssueCategory? Category { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? AssigneeId { get; set; }
    public Guid? ReporterId { get; set; }
    public DateTime? IdentifiedDate { get; set; }
    public DateTime? TargetResolutionDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string? Resolution { get; set; }
    public ICollection<IssueAction> Actions { get; set; } = new List<IssueAction>();
}
