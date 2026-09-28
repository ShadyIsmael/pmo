using NEXUS.Modules.Meetings.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.RiskManagement.Application.DTOs;
using NEXUS.Modules.WorkItems.Application.DTOs;

namespace NEXUS.Modules.ProjectManagement.Application.DTOs;

public sealed class ProjectOverviewDto
{
    public ProjectOverviewProgramDto? Program { get; init; }
    public ProjectStatusReportOverviewDto StatusReports { get; init; } = new();
    public ProjectRiskOverviewDto Risks { get; init; } = new();
    public ProjectWorkItemOverviewDto Issues { get; init; } = new();
    public IReadOnlyList<ProjectOverviewMilestoneDto> Milestones { get; init; } = [];
    public ProjectOverviewBaselineDto? CurrentBaseline { get; init; }
    public int PendingChangeRequestCount { get; init; }
    public int PendingHandoverCount { get; init; }
    public ProjectAllocationOverviewDto Allocations { get; init; } = new();
    public ProjectMeetingOverviewDto Meetings { get; init; } = new();
    public decimal? PlanProgress { get; init; }
    public decimal? PlannedValue { get; init; }
    public decimal? EarnedValue { get; init; }
    public decimal? ActualCost { get; init; }
    public string? Currency { get; init; }
    public decimal? ScheduleVariance { get; init; }
    public decimal? CostVariance { get; init; }
    public decimal? SchedulePerformanceIndex { get; init; }
    public decimal? CostPerformanceIndex { get; init; }
    public decimal? CurrentApprovedProjectBudget { get; init; }
    public decimal AllocatedBudget { get; init; }
    public decimal? UnallocatedBudget { get; init; }
    public decimal? ApprovedBudget { get; init; }
    public decimal? OriginalApprovedBudget { get; init; }
    public decimal? RevisedBudget { get; init; }
    public decimal? CommittedCost { get; init; }
    public decimal? ForecastAtCompletion { get; init; }
    public decimal? RemainingBudget { get; init; }
    public decimal? BudgetUtilizationPercentage { get; init; }
    public decimal? ForecastVariance { get; init; }
    public bool HasBudgetData { get; init; }
    public bool HasCostEntryData { get; init; }
    public bool HasCommitmentData { get; init; }
    public bool HasCostManagementData { get; init; }
    public bool HasPendingRevision { get; init; }
}

public sealed class ProjectOverviewProgramDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
}

public sealed class ProjectOverviewMilestoneDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime? DueDate { get; init; }
    public DateTime? CompletedDate { get; init; }
    public string? Status { get; init; }
    public decimal? Progress { get; init; }
}

public sealed class ProjectOverviewBaselineDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Version { get; init; }
    public DateTime? BaselineDate { get; init; }
    public bool IsCurrent { get; init; }
}
