using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.PluginManagement.Domain;

namespace NEXUS.Modules.PluginManagement.Infrastructure;

public class PluginRepository
{
    private readonly DbContext _context;

    public PluginRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<Plugin?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Plugin>()
            .Include(p => p.Dependencies)
            .Include(p => p.TenantPlugins)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }
}
