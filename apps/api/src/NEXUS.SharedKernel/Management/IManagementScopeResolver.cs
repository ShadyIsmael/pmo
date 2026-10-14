namespace NEXUS.SharedKernel.Management;

public interface IManagementScopeResolver
{
    Task<ManagementPerspectiveSnapshot> GetPerspectiveSnapshotAsync(
        Guid userId,
        Guid tenantId,
        ManagementContext? selectedContext = null,
        CancellationToken cancellationToken = default);

    Task<ManagementContext?> ResolveAsync(
        Guid userId,
        Guid tenantId,
        ManagementPerspective? perspective,
        ManagementViewMode mode,
        Guid? portfolioId,
        Guid? programId,
        Guid? projectId,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessPortfolioAsync(Guid userId, Guid tenantId, Guid portfolioId, CancellationToken cancellationToken = default);
    Task<bool> CanAccessProgramAsync(Guid userId, Guid tenantId, Guid programId, CancellationToken cancellationToken = default);
    Task<bool> CanAccessProjectAsync(Guid userId, Guid tenantId, Guid projectId, CancellationToken cancellationToken = default);
    Task<bool> IsTenantAdministratorAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
}
