using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;
using NEXUS.Modules.Portfolio.Application;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IProjectHandoverRequestService
{
    Task<Result<IReadOnlyList<ProjectHandoverManagerDto>>> GetEligibleManagersAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ManagerHandoverCapabilitiesDto>> GetCapabilitiesAsync(Guid projectId, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectHandoverRequestDto>> CreateAsync(Guid projectId, CreateProjectHandoverRequestDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectHandoverRequestDto>> CreateAdminInitiatedAsync(Guid projectId, CreateProjectHandoverRequestDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectHandoverRequestDto>> CreateInitialAssignmentAsync(Guid projectId, CreateProjectHandoverRequestDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProjectHandoverRequestDto>>> GetForProjectAsync(Guid projectId, string userId, CancellationToken cancellationToken = default);
    Task<Result<int>> GetPendingCountForProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProjectHandoverRequestDto>>> GetPendingForMeAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectHandoverRequestDto>> GetExistingRequestDetailsAsync(Guid requestId, string userId, CancellationToken cancellationToken = default);
    Task<Result<ApprovalReviewDto>> GetApprovalReviewAsync(Guid requestId, string userId, CancellationToken cancellationToken = default);
    Task<int> ProcessDueApprovedHandoversAsync(CancellationToken cancellationToken = default);
    Task<Result<ProjectHandoverRequestDto>> AcceptAsync(Guid requestId, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectHandoverRequestDto>> RejectAsync(Guid requestId, RejectProjectHandoverRequestDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectHandoverRequestDto>> CancelAsync(Guid requestId, string userId, CancellationToken cancellationToken = default);
}
