using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Domain;

/// <summary>
/// A deliverable of the plan, optionally linked to a <see cref="PlanWbsNode"/>
/// and/or a <see cref="PlanActivity"/>. Scoped to exactly one PlanVersionId.
/// </summary>
public class PlanDeliverable : BaseEntity
{
    public Guid PlanVersionId { get; set; }
    public PlanVersion? PlanVersion { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public DateTime? DueDate { get; set; }

    public Guid? WbsId { get; set; }
    public Guid? ActivityId { get; set; }
}