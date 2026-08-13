namespace NEXUS.Modules.Identity.Application;

public interface IAuthorizationService
{
    Task<bool> HasPermissionAsync(Guid userId, string resource, string action, CancellationToken cancellationToken = default);
    Task<bool> HasRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
