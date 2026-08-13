using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Documents.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Documents.Application;

public class DocumentService : IDocumentService
{
    private readonly DbContext _context;

    public DocumentService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<Document>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var doc = await _context.Set<Document>()
            .Include(d => d.Versions)
            .Include(d => d.Reviews)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        return doc is null ? Result<Document>.Failure(Error.NotFound()) : Result<Document>.Success(doc);
    }

    public async Task<PagedResult<Document>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Document>().AsQueryable();
        if (projectId.HasValue) query = query.Where(d => d.ProjectId == projectId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(d => d.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Document>(items, page, pageSize, total);
    }
}
