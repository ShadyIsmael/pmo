namespace NEXUS.Modules.ProjectManagement.Application.DTOs;

public sealed class CreateTimeEntryDto
{
    public Guid? ResourceId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? PlanActivityId { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid? ProjectRoleId { get; set; }
    public DateTime WorkDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public int? ActualSeconds { get; set; }
    public int BreakSeconds { get; set; }
    public int PauseSeconds { get; set; }
    public string? Description { get; set; }
}

public sealed class UpdateTimeEntryDto
{
    public Guid? ProjectId { get; set; }
    public Guid? PlanActivityId { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid? ProjectRoleId { get; set; }
    public DateTime? WorkDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public int? ActualSeconds { get; set; }
    public int? BreakSeconds { get; set; }
    public int? PauseSeconds { get; set; }
    public string? Description { get; set; }
    public byte[]? RowVersion { get; set; }
}

public sealed class TimeEntryDto
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid? PlanActivityId { get; set; }
    public string? PlanActivityName { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid? ProjectRoleId { get; set; }
    public Guid? TimerSessionId { get; set; }
    public DateTime WorkDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int ElapsedSeconds { get; set; }
    public int ActualSeconds { get; set; }
    public int BreakSeconds { get; set; }
    public int PauseSeconds { get; set; }
    public decimal ElapsedHours => Math.Round(ElapsedSeconds / 3600m, 2);
    public decimal ActualHours => Math.Round(ActualSeconds / 3600m, 2);
    public string? Description { get; set; }
    public string EntrySource { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class TimeEntryFilterDto
{
    public Guid? ResourceId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? PlanActivityId { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid? ProjectRoleId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? EntrySource { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class ActualWorkBreakdownDto
{
    public Guid? Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal ActualHours { get; set; }
}

public sealed class TimeEntrySummaryDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal ActualHours { get; set; }
    public decimal PlannedHours { get; set; }
    public decimal WorkVarianceHours { get; set; }
    public decimal CapacityHours { get; set; }
    public decimal PlannedUtilization { get; set; }
    public decimal ActualUtilization { get; set; }
    public List<ActualWorkBreakdownDto> ByResource { get; set; } = new();
    public List<ActualWorkBreakdownDto> ByProject { get; set; } = new();
    public List<ActualWorkBreakdownDto> ByPlanActivity { get; set; } = new();
    public List<ActualWorkBreakdownDto> ByWorkItem { get; set; } = new();
    public List<ActualWorkBreakdownDto> ByTeam { get; set; } = new();
    public List<ActualWorkBreakdownDto> ByProjectRole { get; set; } = new();
}
