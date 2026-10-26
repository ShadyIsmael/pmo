using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IProjectChangeRequestService
{
    Task<Result<ProjectChangeRequestDto>> CreateAsync(CreateProjectChangeRequestDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectChangeRequestDto>> UpdateDraftAsync(Guid id, UpdateProjectChangeRequestDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectChangeRequestDto>> SubmitAsync(Guid id, SubmitProjectChangeRequestDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectChangeRequestApprovalOptionsDto>> GetApprovalOptionsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectChangeRequestApprovalDto>> ReassignApprovalAsync(Guid id, Guid assignmentId, ReassignProjectChangeRequestApprovalDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectChangeRequestDto>> RemediateLegacyAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectChangeRequestDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ApprovalReviewDto>> GetApprovalReviewAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProjectChangeRequestDto>>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<PendingProjectChangeApprovalDto>>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<Result<ProjectChangeRequestDto>> ApproveAsync(Guid id, ApproveProjectChangeRequestDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectChangeRequestDto>> RejectAsync(Guid id, RejectProjectChangeRequestDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectChangeRequestDto>> CancelAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}
