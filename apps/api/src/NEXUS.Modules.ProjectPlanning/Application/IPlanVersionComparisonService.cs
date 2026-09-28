using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public interface IPlanVersionComparisonService
{
    Task<Result<PlanVersionComparisonDto>> CompareAsync(
        Guid planId,
        Guid fromVersionId,
        Guid toVersionId,
        CancellationToken cancellationToken = default);
}
