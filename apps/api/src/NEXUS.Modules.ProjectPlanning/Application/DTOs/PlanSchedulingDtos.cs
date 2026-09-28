using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Application.DTOs;

public sealed class PlanSchedulingAnalysisDto
{
    public Guid PlanVersionId { get; set; }
    public bool HasCycle { get; set; }
    public IReadOnlyList<Guid> CriticalPathActivityIds { get; set; } = [];
    public IReadOnlyList<PlanActivitySchedulingMetricsDto> Activities { get; set; } = [];
}

public sealed class PlanActivitySchedulingMetricsDto
{
    public Guid ActivityId { get; set; }
    public decimal? TotalSlack { get; set; }
    public decimal? FreeSlack { get; set; }
    public bool Critical { get; set; }
}

public sealed class PlanSchedulingExplanationDto
{
    public Guid ActivityId { get; set; }
    public PlanSchedulingMode SchedulingMode { get; set; }
    public PlanConstraintType ConstraintType { get; set; }
    public DateTime? ConstraintDate { get; set; }
    public DateTime? Deadline { get; set; }
    public Guid? EffectiveCalendarId { get; set; }
    public bool CalendarOverride { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? Finish { get; set; }
    public int Duration { get; set; }
    public decimal? TotalSlack { get; set; }
    public decimal? FreeSlack { get; set; }
    public bool Critical { get; set; }
    public bool DeadlineExceeded { get; set; }
    public bool HasPredecessors { get; set; }
    public bool HasSuccessors { get; set; }
    public IReadOnlyList<PlanSchedulingReasonDto> Reasons { get; set; } = [];
    public IReadOnlyList<PlanSchedulingIssueDto> Issues { get; set; } = [];
}

public sealed class PlanSchedulingReasonDto
{
    public string Code { get; set; } = string.Empty;
    public Guid? RelatedActivityId { get; set; }
    public PlanDependencyType? DependencyType { get; set; }
    public int? LagDays { get; set; }
    public int? WorkingDays { get; set; }
    public PlanConstraintType? ConstraintType { get; set; }
}

public sealed class PlanSchedulingIssueDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid ActivityId { get; set; }
    public Guid? RelatedActivityId { get; set; }
}
