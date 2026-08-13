using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application;

public class WorkflowService : IWorkflowService
{
    private readonly DbContext _context;

    public WorkflowService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<WorkflowInstance>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var instance = await _context.Set<WorkflowInstance>()
            .Include(w => w.States)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        return instance is null ? Result<WorkflowInstance>.Failure(Error.NotFound()) : Result<WorkflowInstance>.Success(instance);
    }

    public async Task<PagedResult<WorkflowInstance>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<WorkflowInstance>();
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(w => w.StartedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<WorkflowInstance>(items, page, pageSize, total);
    }
}
