using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IStakeholderProjectReadService
{
    Task<Result<PagedResult<StakeholderProjectViewDto>>> GetMyProjectsAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Result<StakeholderProjectViewDto>> GetMyProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
