using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Application;

public class CostService : ICostService
{
    private readonly DbContext _context;

    public CostService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<CostEntry>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Set<CostEntry>().FindAsync(new object[] { id }, cancellationToken);
        return entry is null ? Result<CostEntry>.Failure(Error.NotFound()) : Result<CostEntry>.Success(entry);
    }

    public async Task<PagedResult<CostEntry>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<CostEntry>().AsQueryable();
        if (projectId.HasValue) query = query.Where(c => c.ProjectId == projectId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(c => c.IncurredDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<CostEntry>(items, page, pageSize, total);
    }
}
