using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public class ProjectStatusReport : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string? Status { get; set; }
    public string? Summary { get; set; }
    public string? Accomplishments { get; set; }
    public string? NextSteps { get; set; }
    public string? Blockers { get; set; }
    public decimal? ScheduleVariance { get; set; }
    public decimal? CostVariance { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public Guid? ReportedBy { get; set; }
}
