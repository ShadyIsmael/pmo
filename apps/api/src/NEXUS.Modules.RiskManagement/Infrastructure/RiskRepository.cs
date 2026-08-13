using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.RiskManagement.Domain;

namespace NEXUS.Modules.RiskManagement.Infrastructure;

public class RiskRepository
{
    private readonly DbContext _context;

    public RiskRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<Risk?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Risk>()
            .Include(r => r.Mitigations)
            .Include(r => r.Contingencies)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }
}
