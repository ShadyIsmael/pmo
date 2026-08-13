using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Application;

public interface IUserAdministrationService
{
    Task<PagedResult<UserAdminDto>> GetPagedAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<Result<UserAdminDto>> CreateAsync(CreateUserDto dto, string actorId, CancellationToken cancellationToken = default);
    Task<Result<UserAdminDto>> UpdateAsync(Guid id, UpdateUserDto dto, string actorId, CancellationToken cancellationToken = default);
}
