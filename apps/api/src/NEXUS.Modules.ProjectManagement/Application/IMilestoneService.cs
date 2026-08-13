using NEXUS.SharedKernel;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IMilestoneService
{
    Task<Result<Milestone>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<Milestone>> GetByProjectAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default);
}
