using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public interface ICapacityService
{
    Task<Result<CapacityMatrixDto>> GetMatrixAsync(CapacityFilterDto filter, CancellationToken cancellationToken = default);
    Task<Result<ProjectTeamCapacityOverviewDto>> GetProjectTeamOverviewAsync(
        IReadOnlyCollection<ProjectTeamCapacityMemberInputDto> members,
        CapacityFilterDto filter,
        CancellationToken cancellationToken = default);
    Task<PagedResult<ResourceAvailabilityDto>> GetAvailabilityAsync(CapacityFilterDto filter, CancellationToken cancellationToken = default);
    Task<Result<DemandVsCapacityDto>> GetDemandVsCapacityAsync(CapacityFilterDto filter, CancellationToken cancellationToken = default);
    Task<Result<List<AllocationDto>>> GetGanttDataAsync(CapacityFilterDto filter, CancellationToken cancellationToken = default);
    Task<Result<ResourceAvailabilityDto>> GetResourceAvailabilityAsync(Guid resourceId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}
