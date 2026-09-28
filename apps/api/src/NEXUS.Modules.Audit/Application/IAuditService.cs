using NEXUS.SharedKernel;
using NEXUS.Modules.Audit.Domain;

namespace NEXUS.Modules.Audit.Application;

public interface IAuditService
{
    Task<PagedResult<AuditEntry>> GetPagedAsync(int page, int pageSize, string? entityType = null, string? action = null, CancellationToken cancellationToken = default);
    Task<Result> AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default);
    Task<Result> LogAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
