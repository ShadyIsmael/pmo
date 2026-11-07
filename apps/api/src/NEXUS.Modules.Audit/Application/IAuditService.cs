using NEXUS.SharedKernel;
using NEXUS.Modules.Audit.Domain;

namespace NEXUS.Modules.Audit.Application;

public interface IAuditService
{
    Task<PagedResult<AuditEntry>> GetPagedAsync(int page, int pageSize, string? entityType = null, string? action = null, Guid? entityId = null, IReadOnlyCollection<Guid>? entityIds = null, string? search = null, CancellationToken cancellationToken = default);

    /// <summary>Server-side, project-scoped audit trail with filters and deterministic ordering.</summary>
    Task<AuditTrailPage> GetProjectTrailAsync(ProjectAuditScope scope, Guid tenantId, AuditTrailQuery request, AuditTrailAccess access, CancellationToken cancellationToken = default);

    /// <summary>Available filter values and the first reliably captured event for the project.</summary>
    Task<AuditTrailFacets> GetProjectFacetsAsync(ProjectAuditScope scope, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Available filter values scoped to one business module.</summary>
    Task<AuditTrailFacets> GetProjectFacetsAsync(ProjectAuditScope scope, Guid tenantId, string module, CancellationToken cancellationToken = default);

    Task<Result> AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default);
    Task<Result> LogAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
