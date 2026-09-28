using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application.DTOs;

public sealed class ProjectStatusReportCurrentProgressDto
{
    public decimal? ProjectPlanProgressPercent { get; init; }
}

public sealed class ProjectStatusReportOverviewDto
{
    public int TotalCount { get; init; }
    public int PublishedCount { get; init; }
    public ProjectStatusReportDto? LatestPublishedReport { get; init; }
}

public class ProjectStatusReportListDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public DateTime ReportDate { get; init; }
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public ProjectStatusReportState ReportState { get; init; }
    public string? ProjectLifecycleStatusAtReport { get; init; }
    public string? OverallHealth { get; init; }
    public decimal? OverallProgressPercent { get; init; }
    public string? PreparedByNameSnapshot { get; init; }
    public DateTime? PublishedAt { get; init; }
}

public sealed class ProjectStatusReportDto : ProjectStatusReportListDto
{
    public string? ProjectNameSnapshot { get; init; }
    public string? ProjectCodeSnapshot { get; init; }
    public Guid? ProjectManagerIdAtReport { get; init; }
    public string? ProjectManagerNameSnapshot { get; init; }
    public string? ScheduleHealth { get; init; }
    public string? CostHealth { get; init; }
    public string? ScopeHealth { get; init; }
    public string? QualityHealth { get; init; }
    public string? ResourceHealth { get; init; }
    public string? ExecutiveSummary { get; init; }
    public string? KeyAchievements { get; init; }
    public string? ChallengesConcerns { get; init; }
    public string? NextPeriodPriorities { get; init; }
    public Guid? PreparedByUserId { get; init; }
    public Guid? PublishedByUserId { get; init; }
    public string? PublishedByNameSnapshot { get; init; }
    public DateTime? SourceDataCapturedAt { get; init; }
    public string? CurrencyCodeSnapshot { get; init; }
    public decimal? BudgetAmountSnapshot { get; init; }
    public decimal? ActualCostAmountSnapshot { get; init; }
    public decimal? CommittedCostAmountSnapshot { get; init; }
    public decimal? ForecastCostAmountSnapshot { get; init; }
    public decimal? CostVarianceAmountSnapshot { get; init; }
    public decimal? ScheduleVarianceDaysSnapshot { get; init; }
    public decimal? ScheduleVariancePercentSnapshot { get; init; }
    public Guid? BaselineIdAtReport { get; init; }
    public string? BaselineNameSnapshot { get; init; }
    public int? BaselineVersionSnapshot { get; init; }
    public IReadOnlyList<ProjectStatusReportRiskSnapshotDto> Risks { get; init; } = [];
    public IReadOnlyList<ProjectStatusReportIssueSnapshotDto> Issues { get; init; } = [];
    public IReadOnlyList<ProjectStatusReportChangeRequestSnapshotDto> ChangeRequests { get; init; } = [];
    public byte[] RowVersion { get; init; } = [];
}

public class CreateProjectStatusReportDto
{
    public DateTime ReportDate { get; init; }
    public DateTime PeriodFrom { get; init; }
    public DateTime PeriodTo { get; init; }
    public string? ProjectLifecycleStatusAtReport { get; init; }
    public string? OverallHealth { get; init; }
    public decimal? OverallProgressPercent { get; init; }
    public string? ScheduleHealth { get; init; }
    public string? CostHealth { get; init; }
    public string? ScopeHealth { get; init; }
    public string? QualityHealth { get; init; }
    public string? ResourceHealth { get; init; }
    public string? ExecutiveSummary { get; init; }
    public string? KeyAchievements { get; init; }
    public string? ChallengesConcerns { get; init; }
    public string? NextPeriodPriorities { get; init; }
}

public sealed class UpdateProjectStatusReportDto : CreateProjectStatusReportDto
{
    public byte[]? RowVersion { get; init; }
}

public sealed class ProjectStatusReportRiskSnapshotDto
{
    public Guid SourceRiskId { get; init; }
    public int Rank { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Impact { get; init; }
    public string? Likelihood { get; init; }
    public string RiskLevel { get; init; } = string.Empty;
    public string RiskStatus { get; init; } = string.Empty;
    public decimal? PotentialCost { get; init; }
    public decimal? PotentialDelay { get; init; }
    public string? RiskResponse { get; init; }
    public Guid? OwnerId { get; init; }
    public string? OwnerNameSnapshot { get; init; }
}

public sealed class ProjectStatusReportIssueSnapshotDto
{
    public Guid SourceIssueId { get; init; }
    public int Rank { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Severity { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string IssueStatus { get; init; } = string.Empty;
    public Guid? AssigneeId { get; init; }
    public string? AssigneeNameSnapshot { get; init; }
    public string? Resolution { get; init; }
}

public sealed class ProjectStatusReportChangeRequestSnapshotDto
{
    public Guid SourceChangeRequestId { get; init; }
    public int Rank { get; init; }
    public int Number { get; init; }
    public string ChangeType { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string? Justification { get; init; }
    public string? Impact { get; init; }
    public string ChangeRequestStatus { get; init; } = string.Empty;
    public Guid RequestedBy { get; init; }
    public string? RequestedByNameSnapshot { get; init; }
    public DateTime RequestedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? DecisionNotes { get; init; }
}
