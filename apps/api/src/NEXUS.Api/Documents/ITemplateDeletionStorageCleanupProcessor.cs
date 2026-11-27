namespace NEXUS.Api.Documents;

public interface ITemplateDeletionStorageCleanupProcessor
{
    Task ProcessRequestAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken = default);

    Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default);
}
