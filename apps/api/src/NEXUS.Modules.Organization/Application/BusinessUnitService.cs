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

    public async Task<Result<BusinessUnitResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var bu = await _context.Set<BusinessUnit>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(b => b.Departments)
                .ThenInclude(d => d.Teams)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        return bu is null
            ? Result<BusinessUnitResponseDto>.Failure(Error.NotFound())
            : Result<BusinessUnitResponseDto>.Success(OrganizationResponseMapper.ToResponse(bu, includeTeams: true));
    }

    public async Task<PagedResult<BusinessUnitResponseDto>> GetByOrganizationAsync(Guid organizationId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<BusinessUnit>()
            .AsNoTracking()
            .Where(b => b.OrganizationId == organizationId)
            .Include(b => b.Departments);
        var total = await query.CountAsync(cancellationToken);
        var units = await query
            .OrderBy(unit => unit.Name).ThenBy(unit => unit.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var items = units.Select(unit => OrganizationResponseMapper.ToResponse(unit, includeTeams: false)).ToArray();
        return new PagedResult<BusinessUnitResponseDto>(items, page, pageSize, total);
    }
}
