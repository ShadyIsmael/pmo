using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Domain;

/// <summary>
/// A node in a WBS tree for a <see cref="PlanVersion"/>. The tree is expressed as
/// an adjacency list via <see cref="ParentWbsId"/>; arbitrary depth is allowed.
/// </summary>
public class PlanWbsNode : BaseEntity
{
    public Guid PlanVersionId { get; set; }
    public PlanVersion? PlanVersion { get; set; }

    public Guid? ParentWbsId { get; set; }
    public PlanWbsNode? Parent { get; set; }

    public int SortOrder { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional link to the activity mapped onto this node.</summary>
    public Guid? ActivityId { get; set; }

    /// <summary>Optional link to the deliverable mapped onto this node.</summary>
    public Guid? DeliverableId { get; set; }

    /// <summary>Optional structured display formatting for the WBS row.</summary>
    public PlanTextFormat? TextFormat { get; set; } = PlanTextFormat.Unspecified();
}
