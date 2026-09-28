using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IStakeholderInvitationService
{
    Task<Result<StakeholderInvitationDto>> SendAsync(Guid projectId, Guid stakeholderId, string actorId, CancellationToken cancellationToken = default);
    Task<Result<StakeholderInvitationDto>> GetAsync(Guid projectId, Guid stakeholderId, CancellationToken cancellationToken = default);
    Task<Result<StakeholderInvitationDto>> RevokeAsync(Guid projectId, Guid stakeholderId, string actorId, CancellationToken cancellationToken = default);
    Task<Result<StakeholderInvitationValidationDto>> ValidateAsync(string token, CancellationToken cancellationToken = default);
    Task<Result<StakeholderInvitationAcceptanceDto>> AcceptAsync(string token, AcceptStakeholderInvitationDto dto, CancellationToken cancellationToken = default);
}
