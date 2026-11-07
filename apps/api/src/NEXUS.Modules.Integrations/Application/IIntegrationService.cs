using NEXUS.SharedKernel;

namespace NEXUS.Modules.Integrations.Application;

public interface IIntegrationService
{
    Task<Result<IntegrationConnectionDto>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<IntegrationConnectionDto>> GetPagedAsync(Guid tenantId, int page, int pageSize, CancellationToken cancellationToken = default);
}
