using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Lookups;

public sealed class CurrentProjectManagerLookup(AppDbContext db) : ICurrentProjectManagerLookup
{
    public async Task<bool> IsCurrentAsync(
        Guid tenantId,
        Guid projectId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty || actorId == Guid.Empty) return false;

        var project = await db.Projects.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == projectId && item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => new { item.ProjectManagerId })
            .FirstOrDefaultAsync(cancellationToken);
        if (project is not null && project.ProjectManagerId == actorId) return true;

        return await db.Set<ProjectManagerAssignment>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId
                && item.ProjectId == projectId
                && item.ProjectManagerId == actorId
                && item.IsCurrent
                && !item.IsDeleted,
                cancellationToken);
    }
}
