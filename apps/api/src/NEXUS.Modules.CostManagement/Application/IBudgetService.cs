using NEXUS.SharedKernel;
using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Modules.CostManagement.Application;

public interface IBudgetService
{
    Task<Result<Budget>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<Budget>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, Guid? portfolioId = null, CancellationToken cancellationToken = default);
}
