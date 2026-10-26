using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public interface IPlanService
{
    /// <summary>
    /// Creates a Plan for a project and its initial Working version. A project may
    /// have multiple plans of the same type, but not duplicate names within that type.
    /// </summary>
    Task<Result<PlanDto>> CreateAsync(CreatePlanDto dto, string userId, CancellationToken cancellationToken = default);

    /// <summary>Lists all plans belonging to a project (tenant-isolated).</summary>
    Task<Result<IReadOnlyList<PlanSummaryDto>>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Gets the focused dataset required for the first Project Plan render.</summary>
    Task<Result<PlanningBootstrapDto>> GetBootstrapAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Gets a plan by id, including its version history.</summary>
    Task<Result<PlanDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets the active Working version of a plan, if any.</summary>
    Task<Result<PlanVersionDto>> GetActiveWorkingVersionAsync(Guid planId, CancellationToken cancellationToken = default);
}
