using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public interface IPlanGridCommandHistoryService
{
    bool IsReplaying { get; }

    IDisposable BeginCollection(string operation);

    Task RecordActivityUpdateAsync(
        Guid planId,
        Guid planVersionId,
        string userId,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> before,
        PlanActivity after,
        CancellationToken cancellationToken = default);

    Task RecordDependencyUpdateAsync(
        Guid planId,
        Guid planVersionId,
        string userId,
        Guid successorActivityId,
        IReadOnlyList<PlanGridDependencyEdgeSnapshot> before,
        IReadOnlyList<PlanGridDependencyEdgeSnapshot> after,
        CancellationToken cancellationToken = default);

    Task RecordWbsUpdateAsync(
        Guid planId,
        Guid planVersionId,
        string userId,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> before,
        PlanWbsNode after,
        CancellationToken cancellationToken = default);

    Task CommitCollectionAsync(Guid planId, Guid planVersionId, string userId, CancellationToken cancellationToken = default);

    Task RecordBarrierAsync(Guid planId, Guid planVersionId, string userId, string operation, CancellationToken cancellationToken = default);

    Task<Result<PlanGridCommandHistoryStateDto>> GetStateAsync(Guid projectId, Guid planId, string userId, CancellationToken cancellationToken = default);

    Task<Result<PlanGridCommandHistoryActionDto>> UndoAsync(Guid projectId, Guid planId, Guid commandId, string userId, CancellationToken cancellationToken = default);

    Task<Result<PlanGridCommandHistoryActionDto>> RedoAsync(Guid projectId, Guid planId, Guid commandId, string userId, CancellationToken cancellationToken = default);
}

public sealed record PlanGridDependencyEdgeSnapshot(
    Guid Id,
    Guid ActivityId,
    Guid PredecessorActivityId,
    PlanDependencyType Type,
    int? Lag,
    bool IsDeleted,
    byte[] RowVersion);
