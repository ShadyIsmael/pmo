using NEXUS.SharedKernel;
using NEXUS.Modules.AIPlanning.Domain;

namespace NEXUS.Modules.AIPlanning.Application;

public interface IAIPlanningService
{
    Task<Result<AIPlanRequest>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AIPlanRequest>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Result<AIPlanResponse>> GeneratePlanAsync(PlanGenerationCommand command, CancellationToken cancellationToken = default);
}
