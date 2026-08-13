using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.WorkflowEngine.Domain;

namespace NEXUS.Modules.WorkflowEngine.Infrastructure;

public class WorkflowRepository
{
    private readonly DbContext _context;

    public WorkflowRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowInstance?> GetInstanceByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<WorkflowInstance>()
            .Include(w => w.States)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }
}
