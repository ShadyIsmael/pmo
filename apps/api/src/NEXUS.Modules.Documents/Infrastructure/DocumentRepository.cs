using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Documents.Domain;

namespace NEXUS.Modules.Documents.Infrastructure;

public class DocumentRepository
{
    private readonly DbContext _context;

    public DocumentRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Document>()
            .Include(d => d.Versions)
            .Include(d => d.Reviews)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }
}
