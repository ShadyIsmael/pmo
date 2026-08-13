using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Application;

public class BudgetService : IBudgetService
{
    private readonly DbContext _context;

    public BudgetService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<Budget>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var budget = await _context.Set<Budget>().FindAsync(new object[] { id }, cancellationToken);
        return budget is null ? Result<Budget>.Failure(Error.NotFound()) : Result<Budget>.Success(budget);
    }

    public async Task<PagedResult<Budget>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, Guid? portfolioId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Budget>().AsQueryable();
        if (projectId.HasValue) query = query.Where(b => b.ProjectId == projectId);
        if (portfolioId.HasValue) query = query.Where(b => b.PortfolioId == portfolioId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(b => b.FiscalYear).ThenBy(b => b.FiscalQuarter).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Budget>(items, page, pageSize, total);
    }
}
