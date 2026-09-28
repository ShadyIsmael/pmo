using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Application.DTOs;

/// <summary>
/// Request payload for adding a new <see cref="PlanWbsNode"/> to the active
/// Working version of a plan.
/// </summary>
public class CreatePlanWbsNodeDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional parent node. Must belong to the same (Working) version.</summary>
    public Guid? ParentWbsId { get; set; }

    /// <summary>Optional ordering among siblings. When null, appended after existing siblings.</summary>
    public int? SortOrder { get; set; }

    public PlanTextFormatDto? TextFormat { get; set; }
}

/// <summary>
/// Request payload for updating a <see cref="PlanWbsNode"/>.
/// </summary>
public class UpdatePlanWbsNodeDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional parent node. Null moves the node to the root level.</summary>
    public Guid? ParentWbsId { get; set; }

    public int SortOrder { get; set; }

    public PlanTextFormatDto? TextFormat { get; set; }

    /// <summary>The RowVersion observed by the client; enforces optimistic concurrency.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Projection of a single <see cref="PlanWbsNode"/> including its children,
/// forming the WBS tree.
/// </summary>
public class PlanWbsNodeDto
{
    public Guid Id { get; set; }
    public Guid PlanVersionId { get; set; }
    public Guid? ParentWbsId { get; set; }
    public int SortOrder { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ActivityId { get; set; }
    public Guid? DeliverableId { get; set; }
    public PlanTextFormatDto? TextFormat { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public IReadOnlyList<PlanWbsNodeDto> Children { get; set; } = Array.Empty<PlanWbsNodeDto>();
}
