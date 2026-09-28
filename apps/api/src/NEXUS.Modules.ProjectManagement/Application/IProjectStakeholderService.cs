using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IProjectStakeholderService
{
    Task<Result<IReadOnlyList<ProjectStakeholderDto>>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectStakeholderDto>> AddInternalAsync(Guid projectId, AddInternalProjectStakeholderDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectStakeholderDto>> AddContactAsync(Guid projectId, AddContactProjectStakeholderDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectStakeholderDto>> UpdateAsync(Guid projectId, Guid stakeholderId, UpdateProjectStakeholderDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectStakeholderDto>> RevokeAsync(Guid projectId, Guid stakeholderId, RevokeProjectStakeholderDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> RemoveAsync(Guid projectId, Guid stakeholderId, byte[]? rowVersion, string userId, CancellationToken cancellationToken = default);
}
