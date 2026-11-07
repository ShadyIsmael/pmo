using NEXUS.SharedKernel;

namespace NEXUS.Api.Account;

public interface IAccountService
{
    Task<Result<AccountResponseDto>> GetAsync(Guid userId, Guid tenantId, Guid currentSessionId, CancellationToken cancellationToken = default);
    Task<Result<AccountProfileDto>> UpdateProfileAsync(Guid userId, Guid tenantId, UpdateAccountProfileRequest request, CancellationToken cancellationToken = default);
    Task<Result<ChangeAccountPasswordResponse>> ChangePasswordAsync(Guid userId, Guid tenantId, ChangeAccountPasswordRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AccountSessionDto>>> GetSessionsAsync(Guid userId, Guid tenantId, Guid currentSessionId, CancellationToken cancellationToken = default);
    Task<Result<bool>> RevokeSessionAsync(Guid userId, Guid tenantId, Guid currentSessionId, Guid sessionId, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task<Result<RevokeOtherSessionsResponse>> RevokeOtherSessionsAsync(Guid userId, Guid tenantId, Guid currentSessionId, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
}
