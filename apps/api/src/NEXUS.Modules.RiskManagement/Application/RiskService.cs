using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.RiskManagement.Application;

public class RiskService : IRiskService
{
    private readonly DbContext _context;

    public RiskService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<Risk>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var risk = await _context.Set<Risk>()
            .Include(r => r.Mitigations)
            .Include(r => r.Contingencies)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return risk is null ? Result<Risk>.Failure(Error.NotFound()) : Result<Risk>.Success(risk);
    }

    public async Task<PagedResult<Risk>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Risk>().AsQueryable();
        if (projectId.HasValue) query = query.Where(r => r.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(r => r.IdentifiedDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Risk>(items, page, pageSize, total);
    }
}
