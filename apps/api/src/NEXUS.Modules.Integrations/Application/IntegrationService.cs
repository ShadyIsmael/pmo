using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Integrations.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Integrations.Application;

public class IntegrationService : IIntegrationService
{
    private readonly DbContext _context;

    public IntegrationService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<IntegrationConnection>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var conn = await _context.Set<IntegrationConnection>()
            .Include(c => c.Syncs)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return conn is null ? Result<IntegrationConnection>.Failure(Error.NotFound()) : Result<IntegrationConnection>.Success(conn);
    }

    public async Task<PagedResult<IntegrationConnection>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<IntegrationConnection>();
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(c => c.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<IntegrationConnection>(items, page, pageSize, total);
    }
}
