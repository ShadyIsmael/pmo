using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

public class ProjectStatusReport : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public DateTime ReportDate { get; set; }
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public DateTime StatusDate { get; set; }
    public ProjectStatusReportState ReportState { get; set; } = ProjectStatusReportState.Draft;

    public string? OverallHealth { get; set; }
    public string? ProjectLifecycleStatusAtReport { get; set; }
    public decimal? OverallProgressPercent { get; set; }
    public string? ScheduleHealth { get; set; }
    public string? CostHealth { get; set; }
    public string? ScopeHealth { get; set; }
    public string? QualityHealth { get; set; }
    public string? ResourceHealth { get; set; }
    public string? ExecutiveSummary { get; set; }
    public string? KeyAchievements { get; set; }
    public string? ChallengesConcerns { get; set; }
    public string? NextPeriodPriorities { get; set; }

    public string? ActionsNeeded { get; set; }
    public string? SupportNeeded { get; set; }

    public string? ProjectNameSnapshot { get; set; }
    public string? ProjectCodeSnapshot { get; set; }
    public Guid? ProjectManagerIdAtReport { get; set; }
    public string? ProjectManagerNameSnapshot { get; set; }

    public Guid? PreparedByUserId { get; set; }
    public string? PreparedByNameSnapshot { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public string? PublishedByNameSnapshot { get; set; }
    public DateTime? SourceDataCapturedAt { get; set; }
    public string? PerformanceSnapshotJson { get; set; }
    public int? ReportingConfigurationVersionSnapshot { get; set; }

    public string? CurrencyCodeSnapshot { get; set; }
    public decimal? BudgetAmountSnapshot { get; set; }
    public decimal? ActualCostAmountSnapshot { get; set; }
    public decimal? CommittedCostAmountSnapshot { get; set; }
    public decimal? ForecastCostAmountSnapshot { get; set; }
    public decimal? CostVarianceAmountSnapshot { get; set; }

    public decimal? ActualPaidSnapshot { get; set; }
    public decimal? OutstandingPlannedPaymentsSnapshot { get; set; }
    public decimal? PaymentsDueWithinPeriodSnapshot { get; set; }
    public decimal? RemainingUnpaidAtPeriodEndSnapshot { get; set; }

    public decimal? ScheduleVarianceDaysSnapshot { get; set; }
    public decimal? ScheduleVariancePercentSnapshot { get; set; }
    public Guid? BaselineIdAtReport { get; set; }
    public string? BaselineNameSnapshot { get; set; }
    public int? BaselineVersionSnapshot { get; set; }

    // Legacy columns are retained for safe migration and compatibility with existing snapshot projections.
    public string? Status { get; set; }
    public decimal? ScheduleVariance { get; set; }
    public decimal? CostVariance { get; set; }
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public Guid? ReportedBy { get; set; }

    public ICollection<ProjectStatusReportRiskSnapshot> RiskSnapshots { get; set; } = new List<ProjectStatusReportRiskSnapshot>();
    public ICollection<ProjectStatusReportIssueSnapshot> IssueSnapshots { get; set; } = new List<ProjectStatusReportIssueSnapshot>();
    public ICollection<ProjectStatusReportChangeRequestSnapshot> ChangeRequestSnapshots { get; set; } = new List<ProjectStatusReportChangeRequestSnapshot>();
}

public enum ProjectStatusReportState
{
    Draft,
    Published,
    Generated,
    Submitted,
    Approved,
    Returned
}
