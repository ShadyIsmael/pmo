using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Modules.ResourceManagement.Infrastructure;

public class ResourceRepository
{
    private readonly DbContext _context;

    public ResourceRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<Resource?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Resource>()
            .Include(r => r.Skills).ThenInclude(rs => rs.Skill)
            .Include(r => r.Allocations)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Resource>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<Resource>()
            .Include(r => r.Skills).ThenInclude(rs => rs.Skill)
            .ToListAsync(cancellationToken);
    }
}
