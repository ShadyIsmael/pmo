using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Domain;

/// <summary>
/// A task or a zero-duration milestone within a <see cref="PlanVersion"/>. All
/// activities are scoped to exactly one PlanVersionId.
/// </summary>
public class PlanActivity : BaseEntity
{
    public Guid PlanVersionId { get; set; }
    public PlanVersion? PlanVersion { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public PlanActivityType Type { get; set; } = PlanActivityType.Task;

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>Zero for milestones.</summary>
    public int Duration { get; set; }

    /// <summary>Optional ordering among activities in the version.</summary>
    public int SortOrder { get; set; }

    public decimal? EstimateHours { get; set; }
    public decimal? Progress { get; set; }

    /// <summary>Optional link to the WBS node this activity rolls up to.</summary>
    public Guid? WbsId { get; set; }
}