using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.PluginManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.PluginManagement.Application;

public class PluginRegistryService : IPluginRegistry
{
    private readonly DbContext _context;

    public PluginRegistryService(DbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<Plugin>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Plugin>();
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(p => p.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Plugin>(items, page, pageSize, total);
    }

    public async Task<Result<Plugin>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plugin = await _context.Set<Plugin>()
            .Include(p => p.Dependencies)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return plugin is null ? Result<Plugin>.Failure(Error.NotFound()) : Result<Plugin>.Success(plugin);
    }
}
