using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public class Project : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ProgramId { get; set; }
    public string Status { get; set; } = "Initiation";
    public string Priority { get; set; } = "Medium";
    public string? Category { get; set; }
    public Guid? ProjectManagerId { get; set; }
    public Guid? SponsorId { get; set; }
    public Guid? PortfolioId { get; set; }
    public Guid? DepartmentId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public decimal? Budget { get; set; }
    public string Currency { get; set; } = "GBP";
    public decimal? ActualCost { get; set; }
    public decimal? PlannedValue { get; set; }
    public decimal? EarnedValue { get; set; }
    public decimal? CostVariance { get; set; }
    public decimal? ScheduleVariance { get; set; }
    public decimal Progress { get; set; }
    public string Health { get; set; } = "Green";
    public string RagStatus { get; set; } = "Green";
    public string ConfidenceLevel { get; set; } = "Green";
    public string? Phase { get; set; }
    public string? RiskRating { get; set; }
    public string? BusinessCase { get; set; }
    public string? Notes { get; set; }
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<ProjectPhase> Phases { get; set; } = new List<ProjectPhase>();
    public ICollection<ProjectStakeholder> Stakeholders { get; set; } = new List<ProjectStakeholder>();
    public ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();
    public ICollection<Deliverable> Deliverables { get; set; } = new List<Deliverable>();
    public ICollection<Baseline> Baselines { get; set; } = new List<Baseline>();
    public ICollection<ProjectStatusReport> StatusReports { get; set; } = new List<ProjectStatusReport>();
}
