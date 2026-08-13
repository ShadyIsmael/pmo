using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public interface IPlanWbsService
{
    /// <summary>
    /// Creates a WBS node inside the active Working version of a plan.
    /// </summary>
    Task<Result<PlanWbsNodeDto>> CreateAsync(Guid planId, CreatePlanWbsNodeDto dto, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a WBS node. Only the active Working version is editable; baseline
    /// versions are immutable. Enforces optimistic concurrency via RowVersion.
    /// </summary>
    Task<Result<PlanWbsNodeDto>> UpdateAsync(Guid planId, Guid nodeId, UpdatePlanWbsNodeDto dto, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a WBS node from the active Working version. Enforces optimistic
    /// concurrency via RowVersion.
    /// </summary>
    Task<Result> DeleteAsync(Guid planId, Guid nodeId, byte[] rowVersion, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full WBS tree of the active Working version of a plan.
    /// </summary>
    Task<Result<IReadOnlyList<PlanWbsNodeDto>>> GetTreeAsync(Guid planId, CancellationToken cancellationToken = default);
}