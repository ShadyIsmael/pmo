using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IProjectBaselineService
{
    Task<Result<ProjectBaselineVersionDto>> CreateInitialBaselineAsync(Guid projectId, string userId, CancellationToken cancellationToken = default);
}
