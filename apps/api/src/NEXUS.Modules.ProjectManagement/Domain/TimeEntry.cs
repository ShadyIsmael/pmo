using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

/// <summary>
/// The authoritative source of actual work. Manual entries and the future timer
/// flow will both persist one row here; planned allocations never become actuals.
/// </summary>
public class TimeEntry : BaseEntity
{
    public const string SourceManual = "Manual";
    public const string SourceTimer = "Timer";

    public Guid ResourceId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? PlanActivityId { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid? ProjectRoleId { get; set; }
    public Guid? TimerSessionId { get; set; }
    public TimerSession? TimerSession { get; set; }

    public DateTime WorkDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public int ElapsedSeconds { get; set; }
    public int ActualSeconds { get; set; }
    public int BreakSeconds { get; set; }
    public int PauseSeconds { get; set; }

    public string? Description { get; set; }
    public string EntrySource { get; set; } = SourceManual;
}
