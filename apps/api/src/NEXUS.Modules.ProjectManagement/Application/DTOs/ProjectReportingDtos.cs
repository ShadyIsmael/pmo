namespace NEXUS.Modules.ProjectManagement.Application.DTOs;

public sealed class SaveProjectReportingConfigurationDto
{
    public DateTime EffectiveFrom { get; init; }
    public string ReportingFrequency { get; init; } = "Weekly";
    public string TimeZoneId { get; init; } = "UTC";
    public string CurrencyCode { get; init; } = string.Empty;
    public IReadOnlyList<string> IncludedSections { get; init; } = [];
    public IReadOnlyList<ProjectReportingPlanSelectionInputDto> Plans { get; init; } = [];
}

public sealed class ProjectReportingPlanSelectionInputDto
{
    public Guid PlanId { get; init; }
    public Guid WorkingVersionId { get; init; }
    public Guid BaselineVersionId { get; init; }
    public bool IncludeInConsolidatedReporting { get; init; } = true;
}

public sealed class ProjectReportingConfigurationDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public int Version { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string ReportingFrequency { get; init; } = "Weekly";
    public string TimeZoneId { get; init; } = "UTC";
    public string CurrencyCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsCurrent { get; init; }
    public IReadOnlyList<string> IncludedSections { get; init; } = [];
    public IReadOnlyList<ProjectReportingPlanSelectionDto> Plans { get; init; } = [];
    public string ConfiguredBy { get; init; } = string.Empty;
    public DateTime ConfiguredAt { get; init; }
}

public sealed class ProjectReportingPlanSelectionDto
{
    public Guid PlanId { get; init; }
    public Guid WorkingVersionId { get; init; }
    public int WorkingVersionNumber { get; init; }
    public Guid BaselineVersionId { get; init; }
    public int BaselineVersionNumber { get; init; }
    public string BaselineName { get; init; } = string.Empty;
    public string PlanName { get; init; } = string.Empty;
    public string PlanType { get; init; } = string.Empty;
    public string PlanStatus { get; init; } = string.Empty;
    public bool IncludeInConsolidatedReporting { get; init; }
}

public sealed class ProjectReportingOptionsDto
{
    public Guid ProjectId { get; init; }
    public IReadOnlyList<ProjectReportingPlanOptionDto> Plans { get; init; } = [];
}

public sealed class ProjectReportingPlanOptionDto
{
    public Guid PlanId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string PlanType { get; init; } = string.Empty;
    public string Classification { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid? CurrentWorkingVersionId { get; init; }
    public int? CurrentWorkingVersionNumber { get; init; }
    public IReadOnlyList<ProjectReportingVersionOptionDto> Versions { get; init; } = [];
}

public sealed class ProjectReportingVersionOptionDto
{
    public Guid Id { get; init; }
    public int VersionNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public bool EligibleForReporting { get; init; }
}

public sealed class ProjectPerformanceDto
{
    public Guid ProjectId { get; init; }
    public DateTime StatusDate { get; init; }
    public string Perspective { get; init; } = "Consolidated";
    public string DataQuality { get; init; } = "Unavailable";
    public int? ReportingConfigurationVersion { get; init; }
    public IReadOnlyList<string> IncludedReportSections { get; init; } = [];
    public string? Currency { get; init; }
    public decimal? OverallProgressPercent { get; init; }
    public decimal? PlannedProgressPercent { get; init; }
    public decimal? BudgetAtCompletion { get; init; }
    public decimal? PlannedValue { get; init; }
    public decimal? EarnedValue { get; init; }
    public decimal? ActualCost { get; init; }
    public decimal? PlanAttributedActualCost { get; init; }
    public decimal? UnallocatedProjectActualCost { get; init; }
    public decimal? ScheduleVariance { get; init; }
    public decimal? CostVariance { get; init; }
    public decimal? SchedulePerformanceIndex { get; init; }
    public decimal? CostPerformanceIndex { get; init; }
    public string? PlannedValueUnavailableReason { get; init; }
    public string? EarnedValueUnavailableReason { get; init; }
    public string? ActualCostUnavailableReason { get; init; }
    public string? SchedulePerformanceIndexUnavailableReason { get; init; }
    public string? CostPerformanceIndexUnavailableReason { get; init; }
    public string? CostVarianceUnavailableReason { get; init; }
    public IReadOnlyList<string> DataQualityReasons { get; init; } = [];
    public IReadOnlyList<ProjectPlanPerformanceDto> Plans { get; init; } = [];
    public IReadOnlyList<ProjectReportingPlanOptionDto> AvailablePlans { get; init; } = [];
    public IReadOnlyList<ProjectPerformanceMilestoneDto> Milestones { get; init; } = [];
    public string CriticalPathMode { get; init; } = "ByPlan";
    public string? CriticalPathUnavailableReason { get; init; }
}

public sealed class ProjectPlanPerformanceDto
{
    public Guid PlanId { get; init; }
    public string PlanName { get; init; } = string.Empty;
    public string PlanType { get; init; } = string.Empty;
    public string Classification { get; init; } = string.Empty;
    public string PlanStatus { get; init; } = string.Empty;
    public bool IncludedInOfficialReporting { get; init; }
    public Guid? WorkingVersionId { get; init; }
    public int? WorkingVersionNumber { get; init; }
    public Guid? BaselineVersionId { get; init; }
    public string? BaselineName { get; init; }
    public int? BaselineVersionNumber { get; init; }
    public decimal? PlannedProgressPercent { get; init; }
    public decimal? ActualProgressPercent { get; init; }
    public decimal? BudgetAtCompletion { get; init; }
    public decimal? PlannedValue { get; init; }
    public decimal? EarnedValue { get; init; }
    public decimal? ActualCost { get; init; }
    public decimal? ScheduleVariance { get; init; }
    public decimal? CostVariance { get; init; }
    public decimal? SchedulePerformanceIndex { get; init; }
    public decimal? CostPerformanceIndex { get; init; }
    public string? CostPerformanceIndexUnavailableReason { get; init; }
    public IReadOnlyList<string> DataQualityReasons { get; init; } = [];
}

public sealed class ProjectPerformanceMilestoneDto
{
    public Guid PlanId { get; init; }
    public string PlanName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTime? BaselineDate { get; init; }
    public DateTime? PlannedDate { get; init; }
    public DateTime? ActualCompletionDate { get; init; }
    public decimal? VarianceDays { get; init; }
    public string Status { get; init; } = string.Empty;
}
