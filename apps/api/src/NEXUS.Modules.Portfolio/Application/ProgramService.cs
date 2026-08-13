using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Portfolio.Application;

public class ProgramService : IProgramService
{
    private readonly DbContext _context;

    public ProgramService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<Program>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var program = await _context.Set<Program>()
            .Include(p => p.Portfolio)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return program is null ? Result<Program>.Failure(Error.NotFound()) : Result<Program>.Success(program);
    }

    public async Task<PagedResult<Program>> GetByPortfolioAsync(Guid? portfolioId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Program>().AsQueryable();
        if (portfolioId.HasValue)
        {
            query = query.Where(p => p.PortfolioId == portfolioId.Value);
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<Program>(items, page, pageSize, total);
    }
}
