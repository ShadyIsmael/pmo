using NEXUS.SharedKernel;
using NEXUS.Modules.WorkItems.Domain;

namespace NEXUS.Modules.WorkItems.Application;

public interface IWorkItemService
{
    Task<Result<WorkItem>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<WorkItem>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, string? status = null, string? type = null, CancellationToken cancellationToken = default);
}
