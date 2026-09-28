using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Application;

public interface ICommitmentService
{
    Task<Result<PurchaseCommitmentDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<PurchaseCommitmentDto>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, CancellationToken cancellationToken = default);
    Task<Result<PurchaseCommitmentDto>> CreateAsync(CreateCommitmentDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<PurchaseCommitmentDto>> UpdateAsync(Guid id, UpdateCommitmentDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> CloseAsync(Guid id, byte[] rowVersion, string userId, CancellationToken cancellationToken = default);
    Task<Result> CancelAsync(Guid id, byte[] rowVersion, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, byte[] rowVersion, string userId, CancellationToken cancellationToken = default);
}
