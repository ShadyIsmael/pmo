using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Domain;

/// <summary>
/// An assignment of a resource to an activity within a
/// <see cref="PlanVersion"/>. <see cref="ResourceId"/> references Resource
/// Management by id; only planning attributes are stored here — never the
/// resource master data.
/// </summary>
public class PlanAssignment : BaseEntity
{
    public Guid PlanVersionId { get; set; }
    public PlanVersion? PlanVersion { get; set; }

    public Guid ActivityId { get; set; }

    public Guid ResourceId { get; set; }

    public decimal? AllocationPct { get; set; }
    public decimal? HoursPerWeek { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}