using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Domain;

/// <summary>
/// A dependency edge between two activities within the same
/// <see cref="PlanVersion"/>. Both endpoints must belong to the same version.
/// </summary>
public class PlanDependency : BaseEntity
{
    public Guid PlanVersionId { get; set; }
    public PlanVersion? PlanVersion { get; set; }

    public Guid ActivityId { get; set; }
    public Guid PredecessorActivityId { get; set; }

    public PlanDependencyType Type { get; set; } = PlanDependencyType.FinishToStart;

    /// <summary>Lead/lag offset in days.</summary>
    public int? Lag { get; set; }
}