using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public interface IPlanDependencyService
{
    /// <summary>
    /// Creates a dependency between two activities inside the active Working
    /// version of a plan. Both endpoints must belong to that version; self-,
    /// duplicate and cycle-forming dependencies are rejected.
    /// </summary>
    Task<Result<PlanDependencyDto>> CreateAsync(Guid planId, CreatePlanDependencyDto dto, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a dependency. Only the active Working version is editable; baseline
    /// versions are immutable. Enforces optimistic concurrency via RowVersion.
    /// </summary>
    Task<Result<PlanDependencyDto>> UpdateAsync(Guid planId, Guid dependencyId, UpdatePlanDependencyDto dto, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a dependency from the active Working version. Enforces
    /// optimistic concurrency via RowVersion.
    /// </summary>
    Task<Result> DeleteAsync(Guid planId, Guid dependencyId, byte[] rowVersion, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all dependencies of the active Working version of a plan.
    /// </summary>
    Task<Result<IReadOnlyList<PlanDependencyDto>>> GetByPlanAsync(Guid planId, CancellationToken cancellationToken = default);
}