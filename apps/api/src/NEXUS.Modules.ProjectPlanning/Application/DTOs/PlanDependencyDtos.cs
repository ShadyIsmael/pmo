using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Application.DTOs;

/// <summary>
/// Request payload for adding a new <see cref="PlanDependency"/> between two
/// activities within the active Working version of a plan.
/// </summary>
public class CreatePlanDependencyDto
{
    /// <summary>The successor activity (the activity that depends on the predecessor).</summary>
    public Guid ActivityId { get; set; }

    /// <summary>The predecessor activity that must finish/start before the successor.</summary>
    public Guid PredecessorActivityId { get; set; }

    public PlanDependencyType Type { get; set; } = PlanDependencyType.FinishToStart;

    /// <summary>Lead/lag offset in days. Negative values represent lead.</summary>
    public int? Lag { get; set; }
}

/// <summary>
/// Request payload for updating a <see cref="PlanDependency"/>.
/// </summary>
public class UpdatePlanDependencyDto
{
    public Guid ActivityId { get; set; }
    public Guid PredecessorActivityId { get; set; }
    public PlanDependencyType Type { get; set; } = PlanDependencyType.FinishToStart;
    public int? Lag { get; set; }

    /// <summary>The RowVersion observed by the client; enforces optimistic concurrency.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Projection of a single <see cref="PlanDependency"/>.
/// </summary>
public class PlanDependencyDto
{
    public Guid Id { get; set; }
    public Guid PlanVersionId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid PredecessorActivityId { get; set; }
    public PlanDependencyType Type { get; set; }
    public int? Lag { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class ReplacePlanPredecessorsDto
{
    public Guid ActivityId { get; set; }
    public IReadOnlyList<PlanPredecessorInputDto> Predecessors { get; set; } = [];
}

public sealed class PlanPredecessorInputDto
{
    public Guid PredecessorActivityId { get; set; }
    public PlanDependencyType Type { get; set; } = PlanDependencyType.FinishToStart;
    public int? Lag { get; set; }
}

public sealed class ReplacePlanPredecessorsResultDto
{
    public IReadOnlyList<PlanDependencyDto> Dependencies { get; init; } = [];
    public IReadOnlyList<PlanScheduledActivityStateDto> ScheduledActivities { get; init; } = [];
    public bool SchedulingRecalculated { get; init; }
}

public sealed class PlanScheduledActivityStateDto
{
    public Guid ActivityId { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int Duration { get; init; }
}
