using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public interface IPlanningImportService
{
    Task<Result<PlanImportPreviewDto>> PreviewAsync(Stream file, string fileName, Guid projectId, Guid? targetPlanId, CancellationToken cancellationToken = default);
    Task<Result<PlanImportResultDto>> ImportAsync(Stream file, string fileName, CommitPlanImportDto request, string userId, CancellationToken cancellationToken = default);
}
