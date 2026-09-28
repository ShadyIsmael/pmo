using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Application.DTOs;

public sealed class PlanImportPreviewDto
{
    public string FileName { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string SuggestedPlanName { get; set; } = string.Empty;
    public PlanImportStatisticsDto Statistics { get; set; } = new();
    public IReadOnlyList<PlanImportTaskPreviewDto> Tasks { get; set; } = Array.Empty<PlanImportTaskPreviewDto>();
    public IReadOnlyList<PlanImportDependencyPreviewDto> Dependencies { get; set; } = Array.Empty<PlanImportDependencyPreviewDto>();
    public IReadOnlyList<PlanImportResourcePreviewDto> Resources { get; set; } = Array.Empty<PlanImportResourcePreviewDto>();
    public IReadOnlyList<PlanImportCalendarPreviewDto> Calendars { get; set; } = Array.Empty<PlanImportCalendarPreviewDto>();
    public IReadOnlyList<PlanImportNoticeDto> Warnings { get; set; } = Array.Empty<PlanImportNoticeDto>();
    public IReadOnlyList<PlanImportNoticeDto> UnsupportedFields { get; set; } = Array.Empty<PlanImportNoticeDto>();
}

public sealed class PlanImportStatisticsDto
{
    public int Tasks { get; set; }
    public int Milestones { get; set; }
    public int WbsLevels { get; set; }
    public int Dependencies { get; set; }
    public int Resources { get; set; }
    public int Assignments { get; set; }
    public int Calendars { get; set; }
    public int ActualDates { get; set; }
}

public sealed class PlanImportTaskPreviewDto
{
    public string SourceId { get; set; } = string.Empty;
    public string? ParentSourceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int OutlineLevel { get; set; }
    public int SortOrder { get; set; }
    public bool IsMilestone { get; set; }
    public PlanSchedulingMode SchedulingMode { get; set; }
    public PlanConstraintType ConstraintType { get; set; }
    public DateTime? ConstraintDate { get; set; }
    public DateTime? Deadline { get; set; }
    public int DurationDays { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? Finish { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualFinish { get; set; }
    public decimal? PercentComplete { get; set; }
    public IReadOnlyList<string> Predecessors { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Resources { get; set; } = Array.Empty<string>();
}

public sealed class PlanImportDependencyPreviewDto
{
    public string SuccessorSourceId { get; set; } = string.Empty;
    public string PredecessorSourceId { get; set; } = string.Empty;
    public PlanDependencyType Type { get; set; }
    public int? LagDays { get; set; }
}

public sealed class PlanImportResourcePreviewDto
{
    public string SourceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public Guid? NexusResourceId { get; set; }
    public string MatchStatus { get; set; } = "Unmatched";
    public int AssignmentCount { get; set; }
}

public sealed class PlanImportCalendarPreviewDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int WorkingDays { get; set; }
    public int Holidays { get; set; }
}

public sealed class PlanImportNoticeDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class CommitPlanImportDto
{
    public Guid ProjectId { get; set; }
    public Guid? TargetPlanId { get; set; }
    public string? NewPlanName { get; set; }
    public PlanType? NewPlanType { get; set; }
}

public sealed class PlanImportResultDto
{
    public Guid PlanId { get; set; }
    public Guid PlanVersionId { get; set; }
    public int VersionNumber { get; set; }
    public bool CreatedPlan { get; set; }
    public PlanImportStatisticsDto Statistics { get; set; } = new();
    public IReadOnlyList<PlanImportNoticeDto> Warnings { get; set; } = Array.Empty<PlanImportNoticeDto>();
}
