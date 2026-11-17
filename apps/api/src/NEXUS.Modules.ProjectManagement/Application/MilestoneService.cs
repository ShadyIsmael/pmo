using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Application;

public class MilestoneService : IMilestoneService
{
    private readonly DbContext _context;
    private readonly IManagementContextAccessor? _managementContext;

    public MilestoneService(DbContext context, IManagementContextAccessor? managementContext = null)
    {
        _context = context;
        _managementContext = managementContext;
    }

    public async Task<Result<Milestone>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var projectIds = _managementContext?.Current?.Scope.ProjectIds;
        if (projectIds is null || projectIds.Count == 0)
            return Result<Milestone>.Failure(Error.NotFound());
        var milestone = await _context.Set<Milestone>().FirstOrDefaultAsync(
            item => item.Id == id && projectIds.Contains(item.ProjectId), cancellationToken);
        return milestone is null ? Result<Milestone>.Failure(Error.NotFound()) : Result<Milestone>.Success(milestone);
    }

    public async Task<PagedResult<Milestone>> GetByProjectAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var projectIds = _managementContext?.Current?.Scope.ProjectIds;
        if (projectIds?.Contains(projectId) != true)
            return new PagedResult<Milestone>(Array.Empty<Milestone>(), page, pageSize, 0);
        var query = _context.Set<Milestone>().Where(m => m.ProjectId == projectId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Milestone>(items, page, pageSize, total);
    }
}
