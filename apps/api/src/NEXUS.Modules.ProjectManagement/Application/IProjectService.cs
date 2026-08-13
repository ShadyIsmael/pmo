using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IProjectService
{
    Task<Result<ProjectDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<ProjectSummaryDto>> GetPagedAsync(int page, int pageSize, string? status = null, string? priority = null, Guid? portfolioId = null, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProjectManagerHistoryDto>>> GetManagerHistoryAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectDto>> CreateAsync(CreateProjectDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectDto>> UpdateAsync(Guid id, UpdateProjectDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<Result> ArchiveAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<Result> ReactivateAsync(Guid id, string userId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ProjectMemberDto>>> GetTeamAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectMemberDto>> AddTeamMemberAsync(Guid projectId, CreateProjectMemberDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectMemberDto>> UpdateTeamMemberAsync(Guid projectId, Guid memberId, UpdateProjectMemberDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result> RemoveTeamMemberAsync(Guid projectId, Guid memberId, string userId, CancellationToken cancellationToken = default);
    Task<Result<List<ProjectTeamMemberAllocationDto>>> CheckResourceConflictsAsync(Guid resourceId, DateTime startDate, DateTime endDate, decimal allocationPercentage, Guid? excludeMemberId, CancellationToken cancellationToken = default);
}
