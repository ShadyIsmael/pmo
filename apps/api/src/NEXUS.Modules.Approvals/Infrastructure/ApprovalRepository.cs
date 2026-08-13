using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Approvals.Domain;

namespace NEXUS.Modules.Approvals.Infrastructure;

public class ApprovalRepository
{
    private readonly DbContext _context;

    public ApprovalRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<ApprovalRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<ApprovalRequest>()
            .Include(a => a.Actions)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }
}
