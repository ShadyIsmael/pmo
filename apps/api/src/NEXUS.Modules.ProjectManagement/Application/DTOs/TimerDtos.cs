namespace NEXUS.Modules.ProjectManagement.Application.DTOs;

public sealed class FinalizeTimerSessionDto
{
    public Guid ProjectId { get; set; }
    public Guid? PlanActivityId { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid? ProjectRoleId { get; set; }
    public string? Description { get; set; }
}

public sealed class TimerHeartbeatDto
{
    public bool IsIdle { get; set; }
    public DateTimeOffset? LastActivityAt { get; set; }
}

public sealed class TimerMetricsDto
{
    public int WorkSeconds { get; set; }
    public int BreakSeconds { get; set; }
    public int PauseSeconds { get; set; }
    public int IdleSeconds { get; set; }
    public int BreakCount { get; set; }
    public int WorkSessionCount { get; set; }
    public int LongestWorkSeconds { get; set; }
    public int AverageWorkSeconds { get; set; }
}

public sealed class TimerSegmentDto
{
    public Guid Id { get; set; }
    public string SegmentType { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public int DurationSeconds { get; set; }
}

public sealed class TimerSessionDto
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public DateTimeOffset ServerNow { get; set; }
    public string Timezone { get; set; } = "UTC";
    public TimerMetricsDto Metrics { get; set; } = new();
    public IReadOnlyList<TimerSegmentDto> Segments { get; set; } = Array.Empty<TimerSegmentDto>();
}

public sealed class EligibleProjectDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
