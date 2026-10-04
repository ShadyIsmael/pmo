using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public interface IPlanningImportService
{
    Task<Result<PlanImportPreviewDto>> PreviewAsync(Stream file, string fileName, Guid projectId, Guid? targetPlanId, CancellationToken cancellationToken = default);
    Task<Result<PlanImportResultDto>> ImportAsync(Stream file, string fileName, CommitPlanImportDto request, string userId, CancellationToken cancellationToken = default);
    Task<Result<PlanImportSessionDto>> CreateSessionAsync(Stream file, string fileName, Guid projectId, Guid? targetPlanId, string userId, CancellationToken cancellationToken = default);
    Task<Result<PlanImportSessionDto>> GetSessionAsync(Guid projectId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<Result<PlanImportSessionDto>> ResolveResourceAsync(Guid projectId, Guid sessionId, string sourceResourceId, PlanImportResourceResolutionDto resolution, string userId, CancellationToken cancellationToken = default);
    Task<Result<PlanImportSessionDto>> ResolveAssignmentAsync(Guid projectId, Guid sessionId, string sourceAssignmentId, PlanImportAssignmentResolutionDto resolution, string userId, CancellationToken cancellationToken = default);
    Task<Result<PlanImportSessionDto>> ResolveCalendarAsync(Guid projectId, Guid sessionId, string sourceCalendarId, PlanImportCalendarResolutionDto resolution, string userId, CancellationToken cancellationToken = default);
    Task<Result<PlanImportResultDto>> CommitSessionAsync(Guid projectId, Guid sessionId, CommitPlanImportDto request, string userId, CancellationToken cancellationToken = default);
    Task<Result> CancelSessionAsync(Guid projectId, Guid sessionId, string userId, CancellationToken cancellationToken = default);
}
