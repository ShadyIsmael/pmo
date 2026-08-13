using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Application;

public class BusinessUnitService : IBusinessUnitService
{
    private readonly DbContext _context;

    public BusinessUnitService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<BusinessUnit>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var bu = await _context.Set<BusinessUnit>()
            .Include(b => b.Departments)
                .ThenInclude(d => d.Teams)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        return bu is null ? Result<BusinessUnit>.Failure(Error.NotFound()) : Result<BusinessUnit>.Success(bu);
    }

    public async Task<PagedResult<BusinessUnit>> GetByOrganizationAsync(Guid organizationId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<BusinessUnit>()
            .Where(b => b.OrganizationId == organizationId)
            .Include(b => b.Departments);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<BusinessUnit>(items, page, pageSize, total);
    }
}
