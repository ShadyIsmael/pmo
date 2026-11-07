using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IProjectReportingService
{
    Task<Result<ProjectReportingOptionsDto>> GetOptionsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectReportingConfigurationDto?>> GetConfigurationAsync(Guid projectId, DateTime? effectiveAt = null, CancellationToken cancellationToken = default);
    Task<Result<ProjectReportingConfigurationDto>> SaveConfigurationAsync(Guid projectId, SaveProjectReportingConfigurationDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectPerformanceDto>> GetPerformanceAsync(Guid projectId, DateTime? statusDate = null, string perspective = "Consolidated", Guid? planId = null, Guid? comparePlanId = null, CancellationToken cancellationToken = default);
    Task<Result<ProjectFinancialSummaryDto>> GetFinancialSummaryAsync(Guid projectId, DateTime periodFrom, DateTime periodTo, CancellationToken cancellationToken = default);
}
