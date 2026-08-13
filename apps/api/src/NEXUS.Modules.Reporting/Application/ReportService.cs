using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Reporting.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Reporting.Application;

public class ReportService : IReportService
{
    private readonly DbContext _context;

    public ReportService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<ReportDefinition>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var report = await _context.Set<ReportDefinition>().FindAsync(new object[] { id }, cancellationToken);
        return report is null ? Result<ReportDefinition>.Failure(Error.NotFound()) : Result<ReportDefinition>.Success(report);
    }

    public async Task<PagedResult<ReportDefinition>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<ReportDefinition>();
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(r => r.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<ReportDefinition>(items, page, pageSize, total);
    }
}
