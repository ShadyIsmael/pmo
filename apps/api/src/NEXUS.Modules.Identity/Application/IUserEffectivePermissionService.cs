namespace NEXUS.Modules.Identity.Application;

public interface IUserEffectivePermissionService
{
    Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<string>> GetEffectivePermissionsForOrganizationAsync(Guid userId, Guid tenantId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<string>> GetEffectiveRoleNamesAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
}
