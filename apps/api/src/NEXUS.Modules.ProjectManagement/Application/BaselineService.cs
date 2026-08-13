using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public class BaselineService : IBaselineService
{
    private readonly DbContext _context;

    public BaselineService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<Baseline>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var baseline = await _context.Set<Baseline>().FindAsync(new object[] { id }, cancellationToken);
        return baseline is null ? Result<Baseline>.Failure(Error.NotFound()) : Result<Baseline>.Success(baseline);
    }

    public async Task<PagedResult<Baseline>> GetByProjectAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Baseline>().Where(b => b.ProjectId == projectId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Baseline>(items, page, pageSize, total);
    }
}
