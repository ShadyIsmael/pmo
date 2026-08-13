using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Reporting.Domain;

namespace NEXUS.Modules.Reporting.Infrastructure;

public class ReportRepository
{
    private readonly DbContext _context;

    public ReportRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<ReportDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<ReportDefinition>().FindAsync(new object[] { id }, cancellationToken);
    }
}
