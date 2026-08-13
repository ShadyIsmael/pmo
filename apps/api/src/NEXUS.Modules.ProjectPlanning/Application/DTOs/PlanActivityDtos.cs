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

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>Must be 0 for milestones.</summary>
    public int Duration { get; set; }

    public decimal? EstimateHours { get; set; }
    public decimal? Progress { get; set; }

    /// <summary>Optional WBS node; must belong to the same PlanVersion.</summary>
    public Guid? WbsId { get; set; }

    /// <summary>Optional ordering among activities in the version.</summary>
    public int? SortOrder { get; set; }
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
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Duration { get; set; }
    public decimal? EstimateHours { get; set; }
    public decimal? Progress { get; set; }
    public Guid? WbsId { get; set; }
    public int SortOrder { get; set; }

    /// <summary>The RowVersion observed by the client; enforces optimistic concurrency.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Projection of a single <see cref="PlanActivity"/>.
/// </summary>
public class PlanActivityDto
{
    public Guid Id { get; set; }
    public Guid PlanVersionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PlanActivityType Type { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Duration { get; set; }
    public int SortOrder { get; set; }
    public decimal? EstimateHours { get; set; }
    public decimal? Progress { get; set; }
    public Guid? WbsId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}