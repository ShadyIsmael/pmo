using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Modules.ProjectManagement.Infrastructure;

public class ProjectRepository
{
    private readonly DbContext _context;

    public ProjectRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Project>()
            .Include(p => p.Members)
            .Include(p => p.Phases)
            .Include(p => p.Milestones)
            .Include(p => p.Deliverables)
            .Include(p => p.Baselines)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<Project>()
            .Include(p => p.Members)
            .Include(p => p.Phases)
            .ToListAsync(cancellationToken);
    }
}
