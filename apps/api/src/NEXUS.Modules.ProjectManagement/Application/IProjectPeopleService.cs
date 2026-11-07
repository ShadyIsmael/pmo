using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IProjectPeopleService
{
    Task<bool> IsTeamGroupAssignmentAsync(Guid projectId, Guid memberId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProjectPersonDto>>> GetAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectPersonDto>> AddAsync(Guid projectId, CreateProjectPersonDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectPersonDto>> UpdateAsync(Guid projectId, Guid personId, UpdateProjectPersonDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectPersonDto>> RevokeOrRemoveAsync(Guid projectId, Guid personId, string source, byte[]? rowVersion, string userId, CancellationToken cancellationToken = default);
    Task<Result> ClearSponsorAsync(Guid projectId, string userId, CancellationToken cancellationToken = default);
}
