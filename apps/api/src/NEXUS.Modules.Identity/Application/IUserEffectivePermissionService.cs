namespace NEXUS.Modules.Identity.Application;

public interface IUserEffectivePermissionService
{
    Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
}
