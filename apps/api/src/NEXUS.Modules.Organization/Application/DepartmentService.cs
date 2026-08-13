using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Application;

public interface IDepartmentService
{
    Task<Result<Department>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<Department>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}

public class DepartmentService : IDepartmentService
{
    private readonly DbContext _context;

    public DepartmentService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<Department>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var department = await _context.Set<Department>()
            .Include(d => d.Teams)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        return department is null ? Result<Department>.Failure(Error.NotFound()) : Result<Department>.Success(department);
    }

    public async Task<PagedResult<Department>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Department>().AsQueryable();
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(d => d.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<Department>(items, page, pageSize, total);
    }
}
