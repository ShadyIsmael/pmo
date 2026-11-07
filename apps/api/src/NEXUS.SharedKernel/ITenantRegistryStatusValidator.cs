namespace NEXUS.SharedKernel;

/// <summary>Validates that a tenant claim resolves to an active registered tenant.</summary>
public interface ITenantRegistryStatusValidator
{
    Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
