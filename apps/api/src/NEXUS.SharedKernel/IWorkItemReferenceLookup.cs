namespace NEXUS.SharedKernel;

public sealed record WorkItemReference(Guid Id, string Title, string Type, string Status, Guid? ProjectId);

public interface IWorkItemReferenceLookup
{
    Task<WorkItemReference?> FindAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, WorkItemReference>> GetManyAsync(IEnumerable<Guid> workItemIds, CancellationToken cancellationToken = default);
}
