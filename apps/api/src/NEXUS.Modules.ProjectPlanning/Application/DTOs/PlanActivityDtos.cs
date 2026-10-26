using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Application.DTOs;

/// <summary>
/// Request payload for adding a new <see cref="PlanActivity"/> to the active
/// Working version of a plan.
/// </summary>
public class CreatePlanActivityDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Task or zero-duration Milestone.</summary>
    public PlanActivityType Type { get; set; } = PlanActivityType.Task;

    public PlanSchedulingMode SchedulingMode { get; set; } = PlanSchedulingMode.Automatic;
    public PlanConstraintType ConstraintType { get; set; } = PlanConstraintType.AsSoonAsPossible;
    public DateTime? ConstraintDate { get; set; }
    public DateTime? Deadline { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualFinish { get; set; }
    public Guid? CalendarId { get; set; }

    /// <summary>Must be 0 for milestones.</summary>
    public int Duration { get; set; }

    public decimal? EstimateHours { get; set; }
    public decimal? Progress { get; set; }

    /// <summary>Optional WBS node; must belong to the same PlanVersion.</summary>
    public Guid? WbsId { get; set; }

    /// <summary>Optional ordering among activities in the version.</summary>
    public int? SortOrder { get; set; }

    public PlanTextFormatDto? TextFormat { get; set; }
}

/// <summary>
/// Request payload for updating a <see cref="PlanActivity"/>.
/// </summary>
public class UpdatePlanActivityDto
{
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
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualFinish { get; set; }
    public Guid? CalendarId { get; set; }
    public int Duration { get; set; }
    public decimal? EstimateHours { get; set; }
    public decimal? Progress { get; set; }
    public Guid? WbsId { get; set; }
    public int SortOrder { get; set; }

    public PlanTextFormatDto? TextFormat { get; set; }

    /// <summary>The RowVersion observed by the client; enforces optimistic concurrency.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>A bounded, atomic set of activity updates for the persisted planning grid.</summary>
public sealed class BulkUpdatePlanActivitiesDto
{
    public List<BulkUpdatePlanActivityItemDto> Items { get; set; } = [];
}

public sealed class BulkUpdatePlanActivityItemDto
{
    public Guid ActivityId { get; set; }
    public UpdatePlanActivityDto Update { get; set; } = new();
}

public enum PlanActivityMovePosition
{
    Before,
    After,
    Child,
}

/// <summary>Structural move request for an activity and its WBS node.</summary>
public sealed class MovePlanActivityDto
{
    public Guid? TargetActivityId { get; set; }
    public Guid? TargetWbsId { get; set; }
    public PlanActivityMovePosition Position { get; set; }
    public int? SortOrder { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public byte[] WbsRowVersion { get; set; } = Array.Empty<byte>();
    public byte[] TargetWbsRowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Projection of a single <see cref="PlanActivity"/>.
/// </summary>
public class PlanActivityDto
{
    public Guid Id { get; set; }
    public Guid PlanVersionId { get; set; }
    public Guid? SourceActivityId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PlanActivityType Type { get; set; }
    /// <summary>True when the activity is the calculated owner of a WBS summary row.</summary>
    public bool IsSummaryTask { get; set; }
    public PlanSchedulingMode SchedulingMode { get; set; }
    public PlanConstraintType ConstraintType { get; set; }
    public DateTime? ConstraintDate { get; set; }
    public DateTime? Deadline { get; set; }
    public bool DeadlineExceeded { get; set; }
    public bool ConstraintApplied { get; set; }
    public bool CalendarOverride { get; set; }
    public bool SchedulingConflict { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualFinish { get; set; }
    public Guid? CalendarId { get; set; }
    public int Duration { get; set; }
    public int SortOrder { get; set; }
    public decimal? EstimateHours { get; set; }
    public decimal? Progress { get; set; }
    public Guid? WbsId { get; set; }
    public PlanTextFormatDto? TextFormat { get; set; }
    public decimal? TotalSlack { get; set; }
    public decimal? FreeSlack { get; set; }
    public bool Critical { get; set; }
    public DateTime? BaselineStartDate { get; set; }
    public DateTime? BaselineFinishDate { get; set; }
    public int? BaselineDuration { get; set; }
    public int? StartVarianceDays { get; set; }
    public int? FinishVarianceDays { get; set; }
    public int? DurationVarianceDays { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
