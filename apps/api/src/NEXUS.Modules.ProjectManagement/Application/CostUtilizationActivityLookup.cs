using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.WorkItems.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

/// <summary>
/// Supplies names for activity references already present in the scoped Cost
/// Utilization work slices. It does not add or recalculate work or cost data.
/// </summary>
public sealed class CostUtilizationActivityLookup : ICostUtilizationActivityLookup
{
    private readonly DbContext _context;

    public CostUtilizationActivityLookup(DbContext context) => _context = context;

    public async Task<CostUtilizationActivityReferences> GetNamesAsync(
        Guid tenantId,
        IEnumerable<Guid> planActivityIds,
        IEnumerable<Guid> workItemIds,
        CancellationToken cancellationToken = default)
    {
        var activityIds = planActivityIds.Distinct().ToArray();
        var itemIds = workItemIds.Distinct().ToArray();

        var activities = activityIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _context.Set<PlanActivity>()
                .AsNoTracking()
                .Where(activity => activity.TenantId == tenantId && !activity.IsDeleted && activityIds.Contains(activity.Id))
                .ToDictionaryAsync(activity => activity.Id, activity => activity.Name, cancellationToken);

        var workItems = itemIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _context.Set<WorkItem>()
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && itemIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Title, cancellationToken);

        return new CostUtilizationActivityReferences(activities, workItems);
    }
}
