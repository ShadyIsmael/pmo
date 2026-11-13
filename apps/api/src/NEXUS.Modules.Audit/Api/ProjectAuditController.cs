using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Audit.Application;

namespace NEXUS.Modules.Audit.Api;

/// <summary>
/// Read-only, project-scoped audit trail. Requires the dedicated Audit.View
/// permission; the platform middleware additionally enforces tenant and
/// management (project) scope for the addressed project before this action runs.
/// There is deliberately no write, update or delete endpoint: audit records are
/// append-only and are produced by the owning module's own unit of work.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/audit")]
[Authorize(Policy = "Audit.View")]
public sealed class ProjectAuditController : ControllerBase
{
    private const string FinancialPermission = "ProjectFinancialAuditTrail.View";

    // Audit details are gated by the exact business permission for each record
    // type; one module grant must not disclose another module's audit payload.
    private static readonly string[] DetailPermissions =
    [
        "ChangeRequest.View",
        "StatusReport.View",
        "Handover.View",
        "Stakeholders.Read",
        "Project.View",
        "Document.View"
    ];

    private readonly IAuditService _auditService;
    private readonly IProjectAuditScopeProvider _scopeProvider;

    public ProjectAuditController(IAuditService auditService, IProjectAuditScopeProvider scopeProvider)
    {
        _auditService = auditService;
        _scopeProvider = scopeProvider;
    }

    /// <summary>Chronological, filterable, server-paged project audit trail.</summary>
    [HttpGet]
    public async Task<IActionResult> GetTrail(
        Guid projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? module = null,
        [FromQuery] string? action = null,
        [FromQuery] Guid? actorId = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantId(out var tenantId)) return Unauthorized();

        var scope = await _scopeProvider.ResolveAsync(projectId, tenantId, cancellationToken);
        if (scope is null) return NotFound(new { error = "The requested project was not found." });

        var request = new AuditTrailQuery
        {
            Page = page,
            PageSize = pageSize,
            FromUtc = NormalizeUtc(from),
            ToUtc = NormalizeUtc(to),
            Module = module,
            Action = action,
            ActorId = actorId,
            EntityType = entityType,
            Search = search,
            NewestFirst = !string.Equals(sort, "oldest", StringComparison.OrdinalIgnoreCase)
        };

        var access = ResolveAccess();
        var result = await _auditService.GetProjectTrailAsync(scope, tenantId, request, access, cancellationToken);

        return Ok(new ProjectAuditTrailResponse(
            result.Items,
            result.Page,
            result.PageSize,
            result.Total,
            result.HasMore,
            access.IncludeFinancialDetail,
            access.IncludeGovernanceDetail,
            scope.ProjectPublicId,
            scope.ProjectName));
    }

    /// <summary>Filter values available for this project's trail.</summary>
    [HttpGet("facets")]
    public async Task<IActionResult> GetFacets(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId)) return Unauthorized();

        var scope = await _scopeProvider.ResolveAsync(projectId, tenantId, cancellationToken);
        if (scope is null) return NotFound(new { error = "The requested project was not found." });

        var access = ResolveAccess();
        var facets = await _auditService.GetProjectFacetsAsync(scope, tenantId, cancellationToken);

        return Ok(new ProjectAuditFacetsResponse(
            facets.Modules,
            facets.Actions,
            facets.Actors,
            facets.EntityTypes,
            facets.CoverageStartUtc,
            access.IncludeFinancialDetail,
            access.IncludeGovernanceDetail,
            scope.ProjectPublicId,
            scope.ProjectName));
    }

    private bool TryGetTenantId(out Guid tenantId) =>
        Guid.TryParse(User.FindFirst("tenantId")?.Value, out tenantId);

    private AuditTrailAccess ResolveAccess() => new(
        HasPermission(FinancialPermission),
        DetailPermissions.Where(HasPermission).ToHashSet(StringComparer.Ordinal));

    private bool HasPermission(string permission) =>
        User.HasClaim("permission", permission);

    /// <summary>
    /// Date filters are stored and compared in UTC. Values without an explicit
    /// offset are treated as UTC rather than as server-local time so the trail
    /// stays stable across environments.
    /// </summary>
    private static DateTime? NormalizeUtc(DateTime? value) => value?.Kind switch
    {
        null => null,
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
    };
}
