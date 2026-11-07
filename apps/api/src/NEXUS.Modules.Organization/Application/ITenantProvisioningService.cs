namespace NEXUS.Modules.Organization.Application;

public interface ITenantProvisioningService
{
    Task<IReadOnlyList<TenantSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TenantProvisioningOperationResult> CreateAsync(
        CreateTenantRequest request,
        CancellationToken cancellationToken = default);

    Task<TenantProvisioningOperationResult> AssignAdministratorAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<TenantProvisioningOperationResult> ChangeLifecycleAsync(
        Guid tenantId,
        NEXUS.Modules.Organization.Domain.TenantLifecycleStatus status,
        CancellationToken cancellationToken = default);
}
