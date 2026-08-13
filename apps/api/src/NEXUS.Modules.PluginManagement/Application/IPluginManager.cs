using NEXUS.SharedKernel;

namespace NEXUS.Modules.PluginManagement.Application;

public interface IPluginManager
{
    Task<Result> ActivateAsync(Guid pluginId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result> DeactivateAsync(Guid pluginId, Guid tenantId, CancellationToken cancellationToken = default);
}
