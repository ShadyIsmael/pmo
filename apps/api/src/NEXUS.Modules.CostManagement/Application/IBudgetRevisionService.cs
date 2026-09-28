using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Application;

public interface IBudgetRevisionService
{
    Task<Result<BudgetRevisionDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<BudgetRevisionDto>> GetByBudgetIdAsync(Guid budgetId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<BudgetRevisionDto>> GetByProjectIdAsync(Guid projectId, int page, int pageSize, string? status = null, CancellationToken cancellationToken = default);
    Task<Result<BudgetRevisionDto>> CreateAsync(CreateBudgetRevisionDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<BudgetRevisionDto>> UpdateAsync(Guid id, UpdateBudgetRevisionDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> SubmitAsync(Guid id, SubmitBudgetRevisionDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> ApproveAsync(Guid id, ReviewBudgetRevisionDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> RejectAsync(Guid id, ReviewBudgetRevisionDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> CancelAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, byte[] rowVersion, string userId, CancellationToken cancellationToken = default);
}
