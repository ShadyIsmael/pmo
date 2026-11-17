using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Audit.Application;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.Audit.Api;

/// <summary>Financial-only project activity for finance readers, backed by the shared append-only audit store.</summary>
[ApiController]
[Route("api/projects/{projectId:guid}/financial-activity")]
[Authorize(Policy = "ProjectFinancialAuditTrail.View")]
public sealed class ProjectFinancialActivityController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly IProjectAuditScopeProvider _scopeProvider;
    private readonly IManagementContextAccessor _management;

    public ProjectFinancialActivityController(
        IAuditService auditService,
        IProjectAuditScopeProvider scopeProvider,
        IManagementContextAccessor management)
    {
        _auditService = auditService;
        _scopeProvider = scopeProvider;
        _management = management;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        Guid projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? eventType = null,
        [FromQuery] Guid? actorId = null,
        [FromQuery] string? reference = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetIdentityScope(projectId, out var tenantId)) return ScopeFailure();

        var scope = await _scopeProvider.ResolveAsync(projectId, tenantId, cancellationToken);
        if (scope is null) return NotFound(new { error = "The requested project was not found." });

        var query = new AuditTrailQuery
        {
            Page = page,
            PageSize = pageSize,
            FromUtc = NormalizeUtc(from, false),
            ToUtc = NormalizeUtc(to, true),
            Module = AuditModuleMap.Finance,
            EntityType = eventType,
            ActorId = actorId,
            Search = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            NewestFirst = true
        };
        var access = new AuditTrailAccess(IncludeFinancialDetail: true,
            DetailPermissionKeys: new HashSet<string>(StringComparer.Ordinal));
        var result = await _auditService.GetProjectTrailAsync(scope, tenantId, query, access, cancellationToken);
        return Ok(new ProjectAuditTrailResponse(
            result.Items,
            result.Page,
            result.PageSize,
            result.Total,
            result.HasMore,
            IncludeFinancialDetail: true,
            IncludeGovernanceDetail: false,
            scope.ProjectPublicId,
            scope.ProjectName));
    }

    [HttpGet("facets")]
    public async Task<IActionResult> GetFacets(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetIdentityScope(projectId, out var tenantId)) return ScopeFailure();

        var scope = await _scopeProvider.ResolveAsync(projectId, tenantId, cancellationToken);
        if (scope is null) return NotFound(new { error = "The requested project was not found." });
        var facets = await _auditService.GetProjectFacetsAsync(scope, tenantId, AuditModuleMap.Finance, cancellationToken);
        return Ok(new ProjectFinancialActivityFacetsResponse(
            facets.EntityTypes,
            facets.Actors,
            facets.CoverageStartUtc));
    }

    private static DateTime? NormalizeUtc(DateTime? value, bool endOfDate)
    {
        if (value is null) return null;
        if (value.Value.Kind == DateTimeKind.Local) return value.Value.ToUniversalTime();
        if (value.Value.Kind == DateTimeKind.Utc) return value.Value;
        var normalized = DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
        return endOfDate && normalized.TimeOfDay == TimeSpan.Zero
            ? normalized.Date.AddDays(1).AddTicks(-1)
            : normalized;
    }

    private bool TryGetIdentityScope(Guid projectId, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var current = _management.Current;
        return current is not null
            && Guid.TryParse(User.FindFirst("tenantId")?.Value, out tenantId)
            && tenantId != Guid.Empty
            && Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorId)
            && actorId != Guid.Empty
            && current.TenantId == tenantId
            && current.UserId == actorId
            && current.Scope.ProjectIds.Contains(projectId);
    }

    private IActionResult ScopeFailure() => Guid.TryParse(User.FindFirst("tenantId")?.Value, out var tenantId)
        && tenantId != Guid.Empty
        ? NotFound(new { error = "The requested project was not found." })
        : Unauthorized();
}

public sealed record ProjectFinancialActivityFacetsResponse(
    IReadOnlyList<string> EventTypes,
    IReadOnlyList<AuditActorOption> Actors,
    DateTime? CoverageStartUtc);
