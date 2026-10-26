using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IProjectOverviewService
{
    Task<Result<ProjectOverviewDto>> GetAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectTeamCapacityOverviewDto>> GetTeamCapacityAsync(
        Guid projectId,
        DateTime startDate,
        DateTime endDate,
        string periodType,
        CancellationToken cancellationToken = default);
}
