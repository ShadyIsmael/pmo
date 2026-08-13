using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Domain;

namespace NEXUS.Modules.Audit.Infrastructure;

public class AuditRepository
{
    private readonly DbContext _context;

    public AuditRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AuditEntry>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<AuditEntry>()
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }
}
