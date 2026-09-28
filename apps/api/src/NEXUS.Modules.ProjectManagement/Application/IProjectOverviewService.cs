using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IProjectOverviewService
{
    Task<Result<ProjectOverviewDto>> GetAsync(Guid projectId, CancellationToken cancellationToken = default);
}
