using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.IssueManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.IssueManagement.Application;

public class IssueService : IIssueService
{
    private readonly DbContext _context;
    private readonly IManagementContextAccessor? _managementContext;

    public IssueService(DbContext context, IManagementContextAccessor? managementContext = null)
    {
        _context = context;
        _managementContext = managementContext;
    }

    public async Task<Result<Issue>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var issue = await ScopedIssues()
            .Include(i => i.Actions)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        return issue is null ? Result<Issue>.Failure(Error.NotFound()) : Result<Issue>.Success(issue);
    }

    public async Task<PagedResult<Issue>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var query = ScopedIssues();
        if (projectId.HasValue) query = query.Where(i => i.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(i => i.Status == status);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(i => i.IdentifiedDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Issue>(items, page, pageSize, total);
    }

    private IQueryable<Issue> ScopedIssues()
    {
        var current = _managementContext?.Current;
        if (current is null || current.TenantId == Guid.Empty || current.Scope.ProjectIds.Count == 0)
            return _context.Set<Issue>().Where(_ => false);

        var tenantId = current.TenantId;
        var projectIds = current.Scope.ProjectIds;
        return _context.Set<Issue>().Where(issue => issue.TenantId == tenantId && !issue.IsDeleted
            && issue.ProjectId.HasValue && projectIds.Contains(issue.ProjectId.Value));
    }
}
