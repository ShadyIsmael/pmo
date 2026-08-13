using NEXUS.SharedKernel;
using NEXUS.Modules.Portfolio.Domain;

namespace NEXUS.Modules.Portfolio.Application;

public interface IProgramService
{
    Task<Result<Program>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<Program>> GetByPortfolioAsync(Guid? portfolioId, int page, int pageSize, CancellationToken cancellationToken = default);
}
