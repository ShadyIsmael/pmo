using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Application;

public interface IAuthenticationService
{
    Task<Result<LoginResponseDto>> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default);
    Task<Result<LoginResponseDto>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(Guid userId, string? refreshToken, CancellationToken cancellationToken = default);
    Task<Result<CurrentUserDto>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<CurrentUserDto>> SwitchRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
}
