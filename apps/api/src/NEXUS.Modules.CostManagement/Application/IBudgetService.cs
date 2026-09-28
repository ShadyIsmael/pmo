using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Application;

public interface IBudgetService
{
    Task<Result<BudgetDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<BudgetDto>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, Guid? portfolioId = null, bool? hasForecast = null, CancellationToken cancellationToken = default);
    Task<Result<BudgetDto>> CreateAsync(CreateBudgetDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<BudgetDto>> UpdateAsync(Guid id, UpdateBudgetDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, byte[] rowVersion, string userId, CancellationToken cancellationToken = default);
}
