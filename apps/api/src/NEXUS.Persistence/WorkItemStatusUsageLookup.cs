using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.WorkItems.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Persistence;

public sealed class WorkItemStatusUsageLookup : IWorkItemStatusUsageLookup
{
    private readonly AppDbContext _context;

    public WorkItemStatusUsageLookup(AppDbContext context)
    {
        _context = context;
    }

    public Task<bool> IsCodeUsedAsync(string code, CancellationToken cancellationToken = default) =>
        _context.Set<WorkItem>().AnyAsync(item => item.Status == code, cancellationToken);
}
