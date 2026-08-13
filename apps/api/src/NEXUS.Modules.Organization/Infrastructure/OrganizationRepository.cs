using Microsoft.EntityFrameworkCore;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;

namespace NEXUS.Modules.Organization.Infrastructure;

public class OrganizationRepository
{
    private readonly DbContext _context;

    public OrganizationRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<OrganizationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<OrganizationEntity>()
            .Include(o => o.BusinessUnits)
                .ThenInclude(b => b.Departments)
                    .ThenInclude(d => d.Teams)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizationEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<OrganizationEntity>()
            .Include(o => o.BusinessUnits)
            .ToListAsync(cancellationToken);
    }
}
