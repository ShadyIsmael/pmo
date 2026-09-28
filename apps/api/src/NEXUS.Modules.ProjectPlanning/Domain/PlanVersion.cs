using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Domain;

/// <summary>
/// One cohesive version of a whole plan. Every plan-content row (activity, WBS
/// node, deliverable, dependency, assignment) belongs to exactly one
/// <see cref="PlanVersion"/> via <c>PlanVersionId</c>.
/// </summary>
public class PlanVersion : BaseEntity
{
    public Guid PlanId { get; set; }
    public Plan? Plan { get; set; }

    /// <summary>1-based, unique within a version type for <c>(TenantId, PlanId)</c>.</summary>
    public int VersionNumber { get; set; }

    public PlanVersionType Type { get; set; }

    public PlanVersionStatus Status { get; set; } = PlanVersionStatus.Active;

    public string? Name { get; set; }

    /// <summary>The Working version a Baseline was snapshotted from.</summary>
    public Guid? SourceVersionId { get; set; }

    /// <summary>The approved Baseline a Working version (draft) was derived from.</summary>
    public Guid? SourceBaselineId { get; set; }

    public ICollection<PlanActivity> Activities { get; set; } = new List<PlanActivity>();
    public ICollection<PlanWbsNode> WbsNodes { get; set; } = new List<PlanWbsNode>();
    public ICollection<PlanDeliverable> Deliverables { get; set; } = new List<PlanDeliverable>();
    public ICollection<PlanDependency> Dependencies { get; set; } = new List<PlanDependency>();
    public ICollection<PlanAssignment> Assignments { get; set; } = new List<PlanAssignment>();
    public ICollection<PlanCalendar> Calendars { get; set; } = new List<PlanCalendar>();
}
