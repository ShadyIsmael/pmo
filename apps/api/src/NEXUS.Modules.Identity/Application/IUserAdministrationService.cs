using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Application;

public interface IUserAdministrationService
{
    Task<Result<PagedResult<UserAdminDto>>> GetPagedAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<RoleDto>>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<Result<CreatedUserDto>> CreateAsync(CreateUserDto dto, string actorId, CancellationToken cancellationToken = default);
    Task<Result<TemporaryPasswordResetDto>> ResetPasswordAsync(Guid id, string actorId, CancellationToken cancellationToken = default);
    Task<Result<UserAdminDto>> UpdateAsync(Guid id, UpdateUserDto dto, string actorId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<UserPermissionAssignmentDto>>> GetUserPermissionsAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<UserPermissionAssignmentDto>>> UpdateUserPermissionsAsync(Guid id, Guid tenantId, UpdateUserPermissionsDto dto, string actorId, CancellationToken cancellationToken = default);
}
