using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Modules.CostManagement.Infrastructure;

public class CostRepository
{
    private readonly DbContext _context;

    public CostRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<Budget?> GetBudgetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Budget>().FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IReadOnlyList<Budget>> GetBudgetsByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Budget>().Where(b => b.ProjectId == projectId).ToListAsync(cancellationToken);
    }
}
