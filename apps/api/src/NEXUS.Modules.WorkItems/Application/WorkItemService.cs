using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.WorkItems.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkItems.Application;

public class WorkItemService : IWorkItemService
{
    private readonly DbContext _context;

    public WorkItemService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<WorkItem>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _context.Set<WorkItem>().FindAsync(new object[] { id }, cancellationToken);
        return item is null ? Result<WorkItem>.Failure(Error.NotFound()) : Result<WorkItem>.Success(item);
    }

    public async Task<PagedResult<WorkItem>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, string? status = null, string? type = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<WorkItem>().AsQueryable();
        if (projectId.HasValue) query = query.Where(w => w.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(w => w.Status == status);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(w => w.Type == type);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(w => w.SortOrder).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<WorkItem>(items, page, pageSize, total);
    }
}
