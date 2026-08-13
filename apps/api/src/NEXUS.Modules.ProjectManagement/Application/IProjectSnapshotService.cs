using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IProjectSnapshotService
{
    Task<Result<IReadOnlyList<ProjectSnapshotSummaryDto>>> GetSnapshotsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectSnapshotDto>> GetSnapshotAsync(Guid projectId, Guid snapshotId, CancellationToken cancellationToken = default);
    Task<Result<ProjectSnapshotDto>> CompleteHandoverAsync(Guid projectId, ProjectHandoverRequest request, string userId, CancellationToken cancellationToken = default);
    Task<Result<ProjectSnapshotSummaryDto>> CreateBaselineSnapshotAsync(Guid projectId, string snapshotType, string snapshotName, string userId, CancellationToken cancellationToken = default);
}
