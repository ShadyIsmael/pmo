using Microsoft.EntityFrameworkCore;
using NEXUS.SharedKernel;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;

namespace NEXUS.Modules.Organization.Application;

public class OrganizationService : IOrganizationService
{
    private readonly DbContext _context;

    public OrganizationService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<OrganizationEntity>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var org = await _context.Set<OrganizationEntity>()
            .Include(o => o.BusinessUnits)
                .ThenInclude(b => b.Departments)
                    .ThenInclude(d => d.Teams)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        return org is null ? Result<OrganizationEntity>.Failure(Error.NotFound()) : Result<OrganizationEntity>.Success(org);
    }

    public async Task<PagedResult<OrganizationEntity>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<OrganizationEntity>()
            .Include(o => o.BusinessUnits);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<OrganizationEntity>(items, page, pageSize, total);
    }
}
