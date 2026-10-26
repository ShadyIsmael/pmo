using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IProjectStatusReportService
{
    Task<Result<PagedResult<ProjectStatusReportListDto>>> GetPagedAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Result<ProjectStatusReportOverviewDto>> GetOverviewAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectStatusReportCurrentProgressDto>> GetCurrentProgressAsync(Guid projectId, DateTime? statusDate = null, CancellationToken cancellationToken = default);
    Task<Result<ProjectStatusReportDto>> GetByIdAsync(Guid projectId, Guid reportId, CancellationToken cancellationToken = default);
    Task<Result<ProjectStatusReportComparisonDto>> ComparePublishedReportsAsync(Guid projectId, Guid firstReportId, Guid secondReportId, CancellationToken cancellationToken = default);
    Task<Result<ProjectStatusReportDto>> CreateAsync(Guid projectId, CreateProjectStatusReportDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectStatusReportDto>> UpdateAsync(Guid projectId, Guid reportId, UpdateProjectStatusReportDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectStatusReportDto>> GenerateAsync(Guid projectId, Guid reportId, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectStatusReportDto>> SubmitAsync(Guid projectId, Guid reportId, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectStatusReportDto>> ApproveAsync(Guid projectId, Guid reportId, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectStatusReportDto>> ReturnAsync(Guid projectId, Guid reportId, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectStatusReportDto>> ReopenAsync(Guid projectId, Guid reportId, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectStatusReportDto>> PublishAsync(Guid projectId, Guid reportId, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid projectId, Guid reportId, string userId, CancellationToken cancellationToken = default);
}
