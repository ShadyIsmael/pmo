using NEXUS.SharedKernel;
using NEXUS.Modules.Meetings.Domain;

namespace NEXUS.Modules.Meetings.Application;

public interface IMeetingService
{
    Task<Result<Meeting>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<Meeting>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, CancellationToken cancellationToken = default);
}
