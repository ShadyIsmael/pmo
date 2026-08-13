using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.WorkItems.Domain;

namespace NEXUS.Modules.WorkItems.Infrastructure;

public class WorkItemRepository
{
    private readonly DbContext _context;

    public WorkItemRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<WorkItem>().FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkItem>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<WorkItem>()
            .Where(w => w.ProjectId == projectId)
            .OrderBy(w => w.SortOrder)
            .ToListAsync(cancellationToken);
    }
}
