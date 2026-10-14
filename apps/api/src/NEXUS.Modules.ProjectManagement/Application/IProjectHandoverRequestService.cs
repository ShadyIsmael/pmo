using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IProjectHandoverRequestService
{
    Task<Result<IReadOnlyList<ProjectHandoverManagerDto>>> GetEligibleManagersAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectHandoverRequestDto>> CreateAsync(Guid projectId, CreateProjectHandoverRequestDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProjectHandoverRequestDto>>> GetForProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProjectHandoverRequestDto>>> GetPendingForMeAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectHandoverRequestDto>> AcceptAsync(Guid requestId, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectHandoverRequestDto>> RejectAsync(Guid requestId, RejectProjectHandoverRequestDto dto, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectHandoverRequestDto>> CancelAsync(Guid requestId, string userId, CancellationToken cancellationToken = default);
}
