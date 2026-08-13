using NEXUS.SharedKernel;
using NEXUS.Modules.RiskManagement.Domain;

namespace NEXUS.Modules.RiskManagement.Application;

public interface IRiskService
{
    Task<Result<Risk>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<Risk>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, string? status = null, CancellationToken cancellationToken = default);
}
