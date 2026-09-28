using NEXUS.Modules.ProjectPlanning.Domain.Scheduling;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public sealed record SchedulingSeed(Guid ActivityId, SchedulingAnchor Anchor = SchedulingAnchor.None);
public sealed record PlanSchedulingResult(int RecalculatedActivityCount, IReadOnlyList<PlanScheduledActivityStateDto> ChangedActivities);

public interface IPlanSchedulingService
{
    Task<Result<PlanSchedulingResult>> RecalculateAsync(
        Guid planVersionId,
        IReadOnlyCollection<SchedulingSeed> seeds,
        string userId,
        CancellationToken cancellationToken = default);
}
