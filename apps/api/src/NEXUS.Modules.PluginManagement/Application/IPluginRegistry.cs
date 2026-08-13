using NEXUS.SharedKernel;
using NEXUS.Modules.PluginManagement.Domain;

namespace NEXUS.Modules.PluginManagement.Application;

public interface IPluginRegistry
{
    Task<PagedResult<Plugin>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Result<Plugin>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
