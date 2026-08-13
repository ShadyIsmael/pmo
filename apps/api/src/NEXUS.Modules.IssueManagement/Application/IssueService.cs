using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.IssueManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.IssueManagement.Application;

public class IssueService : IIssueService
{
    private readonly DbContext _context;

    public IssueService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<Issue>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var issue = await _context.Set<Issue>()
            .Include(i => i.Actions)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        return issue is null ? Result<Issue>.Failure(Error.NotFound()) : Result<Issue>.Success(issue);
    }

    public async Task<PagedResult<Issue>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Issue>().AsQueryable();
        if (projectId.HasValue) query = query.Where(i => i.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(i => i.Status == status);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(i => i.IdentifiedDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Issue>(items, page, pageSize, total);
    }
}
