namespace NEXUS.Modules.CostManagement.Application;

/// <summary>
/// Resolves the functional "current Project Manager" actor independently of
/// broad administrative permissions. Implemented by the persistence layer so
/// the Cost Management application module does not depend on module internals.
/// </summary>
public interface ICurrentProjectManagerLookup
{
    Task<bool> IsCurrentAsync(
        Guid tenantId,
        Guid projectId,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
