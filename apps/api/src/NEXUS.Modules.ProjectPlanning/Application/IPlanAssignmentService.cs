using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public interface IPlanAssignmentService
{
    /// <summary>Creates an assignment in a plan's active Working version.</summary>
    Task<Result<PlanAssignmentDto>> CreateAsync(Guid planId, CreatePlanAssignmentDto dto, string userId, CancellationToken cancellationToken = default);

    /// <summary>Updates an assignment in a plan's active Working version.</summary>
    Task<Result<PlanAssignmentDto>> UpdateAsync(Guid planId, Guid assignmentId, UpdatePlanAssignmentDto dto, string userId, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes an assignment from a plan's active Working version.</summary>
    Task<Result> DeleteAsync(Guid planId, Guid assignmentId, byte[] rowVersion, string userId, CancellationToken cancellationToken = default);

    /// <summary>Lists assignments in a plan's active Working version.</summary>
    Task<Result<IReadOnlyList<PlanAssignmentDto>>> GetByPlanAsync(Guid planId, CancellationToken cancellationToken = default);
}
