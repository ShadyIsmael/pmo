using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Domain;

/// <summary>
/// A task or a zero-duration milestone within a <see cref="PlanVersion"/>. All
/// activities are scoped to exactly one PlanVersionId.
/// </summary>
public class PlanActivity : BaseEntity
{
    public Guid PlanVersionId { get; set; }
    public PlanVersion? PlanVersion { get; set; }

    /// <summary>
    /// Stable activity lineage across Working and Baseline versions. The first
    /// activity in a plan may leave this null; clones point to the original
    /// activity identity and never form a clone-to-clone chain.
    /// </summary>
    public Guid? SourceActivityId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public PlanActivityType Type { get; set; } = PlanActivityType.Task;

    public PlanSchedulingMode SchedulingMode { get; set; } = PlanSchedulingMode.Automatic;

    public PlanConstraintType ConstraintType { get; set; } = PlanConstraintType.AsSoonAsPossible;
    public DateTime? ConstraintDate { get; set; }
    public DateTime? Deadline { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>Execution history captured from field updates or schedule imports.</summary>
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualFinish { get; set; }

    /// <summary>Optional task-specific calendar; otherwise the plan default is used.</summary>
    public Guid? CalendarId { get; set; }
    public PlanCalendar? Calendar { get; set; }

    /// <summary>Zero for milestones.</summary>
    public int Duration { get; set; }

    /// <summary>Optional ordering among activities in the version.</summary>
    public int SortOrder { get; set; }

    public decimal? EstimateHours { get; set; }
    public decimal? Progress { get; set; }

    /// <summary>Optional link to the WBS node this activity rolls up to.</summary>
    public Guid? WbsId { get; set; }

    /// <summary>Optional structured display formatting for the activity row.</summary>
    public PlanTextFormat? TextFormat { get; set; }
}
