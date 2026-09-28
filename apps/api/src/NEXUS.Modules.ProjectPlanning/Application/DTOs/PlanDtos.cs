using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Application.DTOs;

/// <summary>
/// Request payload for creating a new <see cref="Plan"/> for a project. The
/// initial Working <see cref="PlanVersion"/> is created automatically.
/// </summary>
public class CreatePlanDto
{
    public Guid ProjectId { get; set; }

    /// <summary>Required. Drives ADR-007 governing rules for the plan.</summary>
    public PlanType? PlanType { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Full projection of a <see cref="Plan"/> including its version history.
/// </summary>
public class PlanDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public PlanType PlanType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PlanStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public IReadOnlyList<PlanVersionDto> Versions { get; set; } = Array.Empty<PlanVersionDto>();
}

/// <summary>
/// Lightweight list projection of a <see cref="Plan"/> without version details.
/// </summary>
public class PlanSummaryDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public PlanType PlanType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PlanStatus Status { get; set; }
    public int VersionCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Focused first-render payload for the Project Plan workspace. It intentionally
/// contains only the active plan data needed to render the initial grid.
/// </summary>
public class PlanningBootstrapDto
{
    public IReadOnlyList<PlanSummaryDto> Plans { get; set; } = Array.Empty<PlanSummaryDto>();
    public PlanDto? SelectedPlan { get; set; }
    public PlanVersionDto? WorkingVersion { get; set; }
    public IReadOnlyList<PlanWbsNodeDto> Wbs { get; set; } = Array.Empty<PlanWbsNodeDto>();
    public IReadOnlyList<PlanActivityDto> Activities { get; set; } = Array.Empty<PlanActivityDto>();
    public IReadOnlyList<PlanDependencyDto> Dependencies { get; set; } = Array.Empty<PlanDependencyDto>();
}

/// <summary>
/// Projection of a single <see cref="PlanVersion"/>.
/// </summary>
public class PlanVersionDto
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public int VersionNumber { get; set; }
    public PlanVersionType Type { get; set; }
    public PlanVersionStatus Status { get; set; }
    public string? Name { get; set; }
    public Guid? SourceVersionId { get; set; }
    public Guid? SourceBaselineId { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
