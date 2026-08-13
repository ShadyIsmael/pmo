using NEXUS.SharedKernel;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IBaselineService
{
    Task<Result<Baseline>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<Baseline>> GetByProjectAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default);
}
