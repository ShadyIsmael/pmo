using NEXUS.SharedKernel;
using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Modules.CostManagement.Application;

public interface ICostService
{
    Task<Result<CostEntry>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CostEntry>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, CancellationToken cancellationToken = default);
}
