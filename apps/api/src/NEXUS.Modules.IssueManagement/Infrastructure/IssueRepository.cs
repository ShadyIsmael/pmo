using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.IssueManagement.Domain;

namespace NEXUS.Modules.IssueManagement.Infrastructure;

public class IssueRepository
{
    private readonly DbContext _context;

    public IssueRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<Issue?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Issue>()
            .Include(i => i.Actions)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }
}
