using NEXUS.SharedKernel;

namespace NEXUS.Modules.Portfolio.Application;

public interface IPortfolioService
{
    Task<Result<PortfolioDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<PortfolioDto>> GetPagedAsync(int page, int pageSize, string? search, string? status, CancellationToken cancellationToken = default);
    Task<PagedResult<PortfolioLookupDto>> GetProjectLookupAsync(int page, int pageSize, string? search, string? status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortfolioOrganizationLookupDto>> GetEligibleOrganizationsForCreationAsync(CancellationToken cancellationToken = default);
    Task<Result<PortfolioDto>> CreateAsync(CreatePortfolioDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<PortfolioDto>> UpdateAsync(Guid id, SavePortfolioDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> ArchiveAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<Result> ReactivateAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}
