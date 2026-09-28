using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Audit.Application;

public class AuditService : IAuditService
{
    private readonly DbContext _context;

    public AuditService(DbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<AuditEntry>> GetPagedAsync(int page, int pageSize, string? entityType = null, string? action = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<AuditEntry>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action == action);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(a => a.Timestamp).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<AuditEntry>(items, page, pageSize, total);
    }

    public async Task<Result> LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await AppendAsync(entry, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public Task<Result> AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        _context.Set<AuditEntry>().Add(entry);
        return Task.FromResult(Result.Success());
    }
}
