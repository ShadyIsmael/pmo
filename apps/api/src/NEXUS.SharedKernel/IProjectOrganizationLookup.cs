namespace NEXUS.SharedKernel;

/// <summary>Resolves the active, same-tenant Organization owning a Project's management scope.</summary>
public interface IProjectOrganizationLookup
{
    Task<Guid?> ResolveOrganizationIdAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default);
}
