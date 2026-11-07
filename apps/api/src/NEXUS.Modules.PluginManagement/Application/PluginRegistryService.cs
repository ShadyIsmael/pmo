using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.PluginManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.PluginManagement.Application;

public class PluginRegistryService : IPluginRegistry
{
    private readonly DbContext _context;
    private readonly IManagementContextAccessor? _managementContext;

    public PluginRegistryService(DbContext context, IManagementContextAccessor? managementContext = null)
    {
        _context = context;
        _managementContext = managementContext;
    }

    public async Task<PagedResult<Plugin>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = ScopedPlugins();
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(p => p.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Plugin>(items, page, pageSize, total);
    }

    public async Task<Result<Plugin>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plugin = await ScopedPlugins()
            .Include(p => p.Dependencies)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return plugin is null ? Result<Plugin>.Failure(Error.NotFound()) : Result<Plugin>.Success(plugin);
    }

    private IQueryable<Plugin> ScopedPlugins()
    {
        var tenantId = _managementContext?.Current?.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            return _context.Set<Plugin>().Where(_ => false);

        return _context.Set<Plugin>().Where(plugin => plugin.TenantId == tenantId.Value && !plugin.IsDeleted);
    }
}
