using Microsoft.EntityFrameworkCore;
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;

namespace NEXUS.Modules.Portfolio.Infrastructure;

public class PortfolioRepository
{
    private readonly DbContext _context;

    public PortfolioRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<PortfolioEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<PortfolioEntity>()
            .Include(p => p.Programs)
            .Include(p => p.HealthHistory)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PortfolioEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<PortfolioEntity>()
            .Include(p => p.Programs)
            .ToListAsync(cancellationToken);
    }
}
