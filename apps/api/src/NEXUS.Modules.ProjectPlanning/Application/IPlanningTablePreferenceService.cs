using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public interface IPlanningTablePreferenceService
{
    Task<Result<PlanningTablePreferenceDto>> GetAsync(Guid projectId, Guid planId, string userId, CancellationToken cancellationToken = default);
    Task<Result<PlanningTablePreferenceDto>> SaveAsync(Guid projectId, Guid planId, SavePlanningTablePreferenceDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> ResetAsync(Guid projectId, Guid planId, string userId, CancellationToken cancellationToken = default);
}
