using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Application;

public interface IRoleAdministrationService
{
    Task<Result<IReadOnlyList<RoleSummaryDto>>> GetManageableRolesAsync(Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<RoleSummaryDto>>> GetAssignableRolesAsync(Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<RoleOrganizationOptionDto>>> GetManagedRoleOrganizationsAsync(Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<PermissionCatalogItemDto>>> GetAssignablePermissionsAsync(Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<RoleDetailDto>> GetByIdAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<RoleDetailDto>> CreateAsync(SaveRoleDto dto, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<RoleDetailDto>> UpdateAsync(Guid id, SaveRoleDto dto, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default);
}
