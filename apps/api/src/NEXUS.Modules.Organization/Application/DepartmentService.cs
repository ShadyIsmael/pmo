using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Application;

public interface IDepartmentService
{
    Task<Result<DepartmentResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<DepartmentResponseDto>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}

public class DepartmentService : IDepartmentService
{
    private readonly DbContext _context;

    public DepartmentService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<DepartmentResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var department = await _context.Set<Department>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(d => d.Teams)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        return department is null
            ? Result<DepartmentResponseDto>.Failure(Error.NotFound())
            : Result<DepartmentResponseDto>.Success(OrganizationResponseMapper.ToDepartmentResponse(department));
    }

    public async Task<PagedResult<DepartmentResponseDto>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Department>().AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var departments = await query.OrderBy(d => d.Name).ThenBy(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var items = departments.Select(OrganizationResponseMapper.ToDepartmentResponse).ToArray();
        return new PagedResult<DepartmentResponseDto>(items, page, pageSize, total);
    }
}
