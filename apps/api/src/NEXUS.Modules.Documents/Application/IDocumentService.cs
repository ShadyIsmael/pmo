using NEXUS.SharedKernel;
using NEXUS.Modules.Documents.Domain;

namespace NEXUS.Modules.Documents.Application;

public interface IDocumentService
{
    Task<Result<Document>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<Document>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, CancellationToken cancellationToken = default);
}
