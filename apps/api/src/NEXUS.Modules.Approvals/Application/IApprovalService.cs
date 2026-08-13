using NEXUS.SharedKernel;
using NEXUS.Modules.Approvals.Domain;

namespace NEXUS.Modules.Approvals.Application;

public interface IApprovalService
{
    Task<Result<ApprovalRequest>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<ApprovalRequest>> GetPagedAsync(int page, int pageSize, string? status = null, Guid? userId = null, CancellationToken cancellationToken = default);
}
