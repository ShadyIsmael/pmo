using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Domain;

/// <summary>
/// The shared header and anchor for versioning in the Project Planning module.
/// A plan versions its content through <see cref="PlanVersion"/> rows; there are
/// no unversioned plan-content rows.
/// </summary>
public class Plan : BaseEntity
{
    public Guid ProjectId { get; set; }
    public PlanType PlanType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PlanStatus Status { get; set; } = PlanStatus.Draft;

    public ICollection<PlanVersion> Versions { get; set; } = new List<PlanVersion>();
}