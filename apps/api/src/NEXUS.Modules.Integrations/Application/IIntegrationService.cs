using NEXUS.SharedKernel;
using NEXUS.Modules.Integrations.Domain;

namespace NEXUS.Modules.Integrations.Application;

public interface IIntegrationService
{
    Task<Result<IntegrationConnection>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<IntegrationConnection>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
