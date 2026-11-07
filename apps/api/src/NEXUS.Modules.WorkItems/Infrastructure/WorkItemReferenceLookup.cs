using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.WorkItems.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkItems.Infrastructure;

public sealed class WorkItemReferenceLookup : IWorkItemReferenceLookup
{
    private static readonly string[] LinkableTypes = [nameof(WorkItemType.Task), nameof(WorkItemType.Issue)];
    private readonly DbContext _context;

    public WorkItemReferenceLookup(DbContext context) => _context = context;

    public Task<WorkItemReference?> FindAsync(Guid workItemId, CancellationToken cancellationToken = default) =>
        _context.Set<WorkItem>()
            .Where(item => item.Id == workItemId && LinkableTypes.Contains(item.Type))
            .Select(item => new WorkItemReference(item.Id, item.Title, item.Type, item.Status, item.ProjectId, item.AssignedResourceId, item.AssignedTeamId, item.TenantId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, WorkItemReference>> GetManyAsync(IEnumerable<Guid> workItemIds, CancellationToken cancellationToken = default)
    {
        var ids = workItemIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, WorkItemReference>();

        return await _context.Set<WorkItem>()
            .Where(item => ids.Contains(item.Id) && LinkableTypes.Contains(item.Type))
            .Select(item => new WorkItemReference(item.Id, item.Title, item.Type, item.Status, item.ProjectId, item.AssignedResourceId, item.AssignedTeamId, item.TenantId))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
    }
}
