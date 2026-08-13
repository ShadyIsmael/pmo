using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public interface IPlanActivityService
{
    /// <summary>
    /// Creates an activity inside the active Working version of a plan.
    /// </summary>
    Task<Result<PlanActivityDto>> CreateAsync(Guid planId, CreatePlanActivityDto dto, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an activity. Only the active Working version is editable; baseline
    /// versions are immutable. Enforces optimistic concurrency via RowVersion.
    /// </summary>
    Task<Result<PlanActivityDto>> UpdateAsync(Guid planId, Guid activityId, UpdatePlanActivityDto dto, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes an activity from the active Working version. Enforces optimistic
    /// concurrency via RowVersion.
    /// </summary>
    Task<Result> DeleteAsync(Guid planId, Guid activityId, byte[] rowVersion, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all activities of the active Working version of a plan, ordered by
    /// SortOrder. Suitable for list and tree-ready (WbsId) rendering.
    /// </summary>
    Task<Result<IReadOnlyList<PlanActivityDto>>> GetByPlanAsync(Guid planId, CancellationToken cancellationToken = default);
}