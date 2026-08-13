using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

public class MilestoneService : IMilestoneService
{
    private readonly DbContext _context;

    public MilestoneService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<Milestone>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var milestone = await _context.Set<Milestone>().FindAsync(new object[] { id }, cancellationToken);
        return milestone is null ? Result<Milestone>.Failure(Error.NotFound()) : Result<Milestone>.Success(milestone);
    }

    public async Task<PagedResult<Milestone>> GetByProjectAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Milestone>().Where(m => m.ProjectId == projectId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Milestone>(items, page, pageSize, total);
    }
}
