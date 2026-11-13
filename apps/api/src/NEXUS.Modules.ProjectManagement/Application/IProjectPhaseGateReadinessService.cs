using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public interface IProjectPhaseGateReadinessService
{
    Task<Result<ProjectPhaseGateReadinessDto>> GetAsync(
        Guid projectId,
        Guid? targetPhaseId = null,
        CancellationToken cancellationToken = default);

    Task<Result<ProjectPhaseGateReadinessDto>> LinkDocumentAsync(
        Guid projectId,
        Guid requirementSnapshotId,
        Guid documentId,
        Guid documentVersionId,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<Result<ProjectPhaseGateReadinessDto>> UnlinkDocumentAsync(
        Guid projectId,
        Guid requirementSnapshotId,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<Result<ProjectPhaseTransitionResultDto>> ChangePhaseAsync(
        Guid projectId,
        Guid targetPhaseId,
        string expectedProjectVersionToken,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persisted phase-change history for one project, newest first. Each row is the
    /// immutable audit snapshot captured when the transition happened; it never
    /// depends on the current configuration or lifecycle catalog state.
    /// </summary>
    Task<Result<IReadOnlyList<ProjectPhaseHistoryItemDto>>> GetPhaseHistoryAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}
