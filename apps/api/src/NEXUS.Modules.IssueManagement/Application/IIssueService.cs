using NEXUS.SharedKernel;
using NEXUS.Modules.IssueManagement.Domain;

namespace NEXUS.Modules.IssueManagement.Application;

public interface IIssueService
{
    Task<Result<Issue>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<Issue>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, string? status = null, CancellationToken cancellationToken = default);
}
