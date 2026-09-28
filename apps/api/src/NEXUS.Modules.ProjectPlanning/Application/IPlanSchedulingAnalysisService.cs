using NEXUS.Modules.ProjectPlanning.Domain.Scheduling;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public interface IPlanSchedulingAnalysisService
{
    Task<Result<Guid>> GetActiveWorkingVersionIdAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<Result<CriticalPathAnalysisResult>> AnalyzeAsync(Guid planVersionId, CancellationToken cancellationToken = default);
    Task<Result<SchedulingExplanation>> ExplainAsync(Guid planVersionId, Guid activityId, CancellationToken cancellationToken = default);
}
