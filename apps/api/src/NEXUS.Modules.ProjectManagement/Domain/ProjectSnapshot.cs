using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Domain;

/// <summary>
/// Immutable snapshot of a project taken at a specific point in time (e.g. project manager handover).
/// The header stores the strongly-typed snapshot metadata and frozen core project fields, while
/// <see cref="Data"/> holds a serialized, strongly-typed payload of all project-owned child data
/// (team, plan, tasks, risks, costs, reports, etc.) captured independently from the live project.
/// Snapshots are write-once: no update or delete operations are exposed.
/// </summary>
public class ProjectSnapshot : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public int SnapshotNumber { get; set; }
    public string SnapshotName { get; set; } = string.Empty;
    public string SnapshotType { get; set; } = "ManagerHandover";
    public DateTime SnapshotDate { get; set; }
    public DateTime EffectiveEndDate { get; set; }
    public Guid? PreviousProjectManagerId { get; set; }
    public Guid? NewProjectManagerId { get; set; }
    public string? HandoverReason { get; set; }
    public string? HandoverNotes { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ProgramId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? Category { get; set; }
    public Guid? SponsorId { get; set; }
    public Guid? PortfolioId { get; set; }
    public Guid? DepartmentId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public decimal? Budget { get; set; }
    public string Currency { get; set; } = "GBP";
    public decimal? ActualCost { get; set; }
    public decimal? PlannedValue { get; set; }
    public decimal? EarnedValue { get; set; }
    public decimal? CostVariance { get; set; }
    public decimal? ScheduleVariance { get; set; }
    public decimal Progress { get; set; }
    public string Health { get; set; } = string.Empty;
    public string RagStatus { get; set; } = string.Empty;
    public string ConfidenceLevel { get; set; } = string.Empty;
    public string? Phase { get; set; }
    public string? RiskRating { get; set; }
    public string? BusinessCase { get; set; }
    public string? Notes { get; set; }

    /// <summary>Serialized strongly-typed snapshot payload of project-owned child data.</summary>
    public string? Data { get; set; }
}
