using NEXUS.SharedKernel;

namespace NEXUS.Modules.Integrations.Application;

public interface ISyncProvider
{
    Task<Result> SyncAsync(string provider, string entityType, CancellationToken cancellationToken = default);
}
