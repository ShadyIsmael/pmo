using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Application;

public interface ICostService
{
    Task<Result<CostEntryDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CostEntryDto>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, CancellationToken cancellationToken = default);
    Task<Result<CostEntryDto>> CreateAsync(CreateCostEntryDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<CostEntryDto>> UpdateAsync(Guid id, UpdateCostEntryDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, byte[] rowVersion, string userId, CancellationToken cancellationToken = default);
}
