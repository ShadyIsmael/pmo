using NEXUS.SharedKernel;

namespace NEXUS.Api.PortfolioManagement;

public interface IPortfolioManagementService
{
    Task<PagedResult<PortfolioManagementListItemDto>> GetPortfoliosAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default);
    Task<Result<PortfolioManagementOverviewDto>> GetOverviewAsync(Guid portfolioId, int programsPage, int projectsPage, int pageSize, CancellationToken cancellationToken = default);
}
