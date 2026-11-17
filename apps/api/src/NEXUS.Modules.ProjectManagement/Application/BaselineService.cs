using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Application;

public class BaselineService : IBaselineService
{
    private readonly DbContext _context;
    private readonly IManagementContextAccessor? _managementContext;

    public BaselineService(DbContext context, IManagementContextAccessor? managementContext = null)
    {
        _context = context;
        _managementContext = managementContext;
    }

    public async Task<Result<Baseline>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var projectIds = _managementContext?.Current?.Scope.ProjectIds;
        if (projectIds is null || projectIds.Count == 0)
            return Result<Baseline>.Failure(Error.NotFound());
        var baseline = await _context.Set<Baseline>().FirstOrDefaultAsync(
            item => item.Id == id && projectIds.Contains(item.ProjectId), cancellationToken);
        return baseline is null ? Result<Baseline>.Failure(Error.NotFound()) : Result<Baseline>.Success(baseline);
    }

    public async Task<PagedResult<Baseline>> GetByProjectAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var projectIds = _managementContext?.Current?.Scope.ProjectIds;
        if (projectIds?.Contains(projectId) != true)
            return new PagedResult<Baseline>(Array.Empty<Baseline>(), page, pageSize, 0);
        var query = _context.Set<Baseline>().Where(b => b.ProjectId == projectId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Baseline>(items, page, pageSize, total);
    }
}
