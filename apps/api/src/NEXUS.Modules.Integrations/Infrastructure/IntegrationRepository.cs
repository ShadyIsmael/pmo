using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Integrations.Domain;

namespace NEXUS.Modules.Integrations.Infrastructure;

public class IntegrationRepository
{
    private readonly DbContext _context;

    public IntegrationRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<IntegrationConnection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<IntegrationConnection>()
            .Include(c => c.Syncs)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }
}
