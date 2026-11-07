using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Management;

public sealed class ManagementContextMiddleware
{
    public const string PerspectiveHeader = "X-Nexus-Perspective";
    public const string ViewModeHeader = "X-Nexus-View-Mode";
    public const string PortfolioHeader = "X-Nexus-Portfolio-Id";
    public const string ProgramHeader = "X-Nexus-Program-Id";
    public const string ProjectHeader = "X-Nexus-Project-Id";
    public const string ReadOnlyMessage = "This action is not available while using read-only perspective mode.";

    private readonly RequestDelegate _next;
    public ManagementContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext httpContext, IManagementScopeResolver resolver, IManagementContextAccessor accessor, ILogger<ManagementContextMiddleware> logger, IWebHostEnvironment environment)
    {
        var isProjectShellDiagnostic = environment.IsDevelopment() && IsProjectShellRequest(httpContext.Request.Path);
        var hasPerspectiveHeader = httpContext.Request.Headers.ContainsKey(PerspectiveHeader);
        var hasViewModeHeader = httpContext.Request.Headers.ContainsKey(ViewModeHeader);
        var hasProjectHeader = httpContext.Request.Headers.ContainsKey(ProjectHeader);
        if (isProjectShellDiagnostic)
        {
            logger.LogInformation(
                "Project Shell management-context processing started. RequestId={RequestId} Authenticated={Authenticated} PerspectiveHeaderPresent={PerspectiveHeaderPresent} ViewModeHeaderPresent={ViewModeHeaderPresent} ProjectIdHeaderPresent={ProjectIdHeaderPresent}",
                httpContext.TraceIdentifier,
                httpContext.User.Identity?.IsAuthenticated == true,
                hasPerspectiveHeader,
                hasViewModeHeader,
                hasProjectHeader);
        }

        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            if (isProjectShellDiagnostic)
            {
                logger.LogInformation(
                    "Project Shell management-context processing continued without an authenticated principal. RequestId={RequestId} RejectionReason={RejectionReason}",
                    httpContext.TraceIdentifier,
                    "EndpointAuthorizationMustRejectUnauthenticatedRequest");
            }

            await _next(httpContext);
            return;
        }

        var userIdResolved = Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty;
        var tenantIdResolved = Guid.TryParse(httpContext.User.FindFirstValue("tenantId"), out var tenantId) && tenantId != Guid.Empty;
        if (!userIdResolved || !tenantIdResolved)
        {
            if (isProjectShellDiagnostic)
            {
                logger.LogWarning(
                    "Project Shell management context could not resolve trusted identity claims. RequestId={RequestId} UserIdResolved={UserIdResolved} TenantIdResolved={TenantIdResolved} RejectionReason={RejectionReason}",
                    httpContext.TraceIdentifier,
                    userIdResolved,
                    tenantIdResolved,
                    "AuthenticatedPrincipalMissingValidUserOrTenantClaim");
            }

            await _next(httpContext);
            return;
        }

        if (isProjectShellDiagnostic)
        {
            logger.LogInformation(
                "Project Shell trusted identity claims resolved. RequestId={RequestId} UserId={UserId} TenantId={TenantId} UserIdResolved={UserIdResolved} TenantIdResolved={TenantIdResolved}",
                httpContext.TraceIdentifier,
                userId,
                tenantId,
                userIdResolved,
                tenantIdResolved);
        }

        var rawPerspective = httpContext.Request.Headers[PerspectiveHeader].FirstOrDefault();
        var rawViewMode = httpContext.Request.Headers[ViewModeHeader].FirstOrDefault();
        var perspective = ParseEnum<ManagementPerspective>(rawPerspective);
        var parsedViewMode = ParseEnum<ManagementViewMode>(rawViewMode);
        if ((!string.IsNullOrWhiteSpace(rawPerspective) && !perspective.HasValue) || (!string.IsNullOrWhiteSpace(rawViewMode) && !parsedViewMode.HasValue))
        {
            if (isProjectShellDiagnostic)
            {
                logger.LogWarning(
                    "Project Shell management context rejected invalid optional context headers. RequestId={RequestId} UserId={UserId} TenantId={TenantId} PerspectiveHeaderPresent={PerspectiveHeaderPresent} ViewModeHeaderPresent={ViewModeHeaderPresent} RejectionReason={RejectionReason}",
                    httpContext.TraceIdentifier,
                    userId,
                    tenantId,
                    hasPerspectiveHeader,
                    hasViewModeHeader,
                    "InvalidPerspectiveOrViewModeHeader");
            }

            await ForbiddenAsync(httpContext, "The requested perspective or context is not available.");
            return;
        }
        var viewMode = parsedViewMode ?? ManagementViewMode.Native;
        var hasAssignmentRecoveryPermission = viewMode == ManagementViewMode.Native
            && IsAssignmentRecoveryPath(httpContext.Request.Path)
            && httpContext.User.HasClaim("permission", AssignmentRecoveryPermissions.View);
        var canReadAllTenantPortfolioProgramData = HttpMethods.IsGet(httpContext.Request.Method)
            && viewMode == ManagementViewMode.Native
            && IsTenantAdminPortfolioProgramReadPath(httpContext.Request.Path)
            && await resolver.IsTenantAdministratorAsync(userId, tenantId, httpContext.RequestAborted);
        var hasTenantAdministratorPortfolioWriteScope = HttpMethods.IsPut(httpContext.Request.Method)
            && viewMode == ManagementViewMode.Native
            && IsTenantAdminPortfolioUpdatePath(httpContext.Request.Path)
            && await resolver.IsTenantAdministratorAsync(userId, tenantId, httpContext.RequestAborted);
        var hasTenantPortfolioAdminScope = canReadAllTenantPortfolioProgramData || hasTenantAdministratorPortfolioWriteScope;
        var context = await resolver.ResolveAsync(userId, tenantId, perspective, viewMode, ParseGuid(httpContext.Request.Headers[PortfolioHeader]), ParseGuid(httpContext.Request.Headers[ProgramHeader]), ParseGuid(httpContext.Request.Headers[ProjectHeader]), httpContext.RequestAborted);
        if (context is null && (perspective.HasValue || viewMode == ManagementViewMode.ReadOnlyViewAs)
            && !hasTenantPortfolioAdminScope && !hasAssignmentRecoveryPermission)
        {
            // Timer sessions are personal to the authenticated Resource owner
            // and are intentionally started before a project is selected.
            // A stale native management header must not block this flow. The
            // service still enforces TimeTracking permissions and tenant/user
            // ownership, while read-only view-as remains blocked below.
            if (IsTimerEndpoint(httpContext.Request.Path) && viewMode == ManagementViewMode.Native)
            {
                await _next(httpContext);
                return;
            }

            if (isProjectShellDiagnostic)
            {
                logger.LogWarning(
                    "Project Shell management context was rejected before resource-scope evaluation. RequestId={RequestId} UserId={UserId} TenantId={TenantId} RequestedPerspective={RequestedPerspective} EffectiveViewMode={EffectiveViewMode} RejectionReason={RejectionReason}",
                    httpContext.TraceIdentifier,
                    userId,
                    tenantId,
                    perspective?.ToString(),
                    viewMode.ToString(),
                    "RequestedPerspectiveOrReadOnlyContextUnavailable");
            }

            await ForbiddenAsync(httpContext, "The requested perspective or context is not available.");
            return;
        }

        var usedNoAssignmentFallback = context is null;
        if (context is null)
        {
            // No perspective header was sent and the user has no native
            // assignments. Establish a context with the user's native scope
            // (empty when no assignments exist) so that row-level scope
            // filtering in services (e.g. ProjectService.InScope) is applied
            // instead of silently falling open to every tenant record.
            var snapshot = await resolver.GetPerspectiveSnapshotAsync(userId, tenantId, cancellationToken: httpContext.RequestAborted);
            context = new ManagementContext(userId, tenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native, null, null, null, snapshot.Scope)
            {
                CanReadAllTenantPortfolioProgramData = canReadAllTenantPortfolioProgramData,
                HasTenantAdministratorPortfolioWriteScope = hasTenantAdministratorPortfolioWriteScope
            };
        }
        else if (hasTenantPortfolioAdminScope)
        {
            context = context with
            {
                CanReadAllTenantPortfolioProgramData = canReadAllTenantPortfolioProgramData,
                HasTenantAdministratorPortfolioWriteScope = hasTenantAdministratorPortfolioWriteScope
            };
        }

        if (context is not null)
        {
            accessor.Set(context);
            if (isProjectShellDiagnostic)
            {
                logger.LogInformation(
                    "Project Shell management context resolved. RequestId={RequestId} UserId={UserId} TenantId={TenantId} PerspectiveHeaderPresent={PerspectiveHeaderPresent} ViewModeHeaderPresent={ViewModeHeaderPresent} ProjectIdHeaderPresent={ProjectIdHeaderPresent} RequestedPerspective={RequestedPerspective} EffectivePerspective={EffectivePerspective} EffectiveViewMode={EffectiveViewMode} DefaultPerspectiveApplied={DefaultPerspectiveApplied} NoAssignmentFallbackApplied={NoAssignmentFallbackApplied} ScopeProjectCount={ScopeProjectCount}",
                    httpContext.TraceIdentifier,
                    userId,
                    tenantId,
                    hasPerspectiveHeader,
                    hasViewModeHeader,
                    hasProjectHeader,
                    perspective?.ToString(),
                    context.SelectedPerspective.ToString(),
                    context.ViewMode.ToString(),
                    !perspective.HasValue,
                    usedNoAssignmentFallback,
                    context.Scope.ProjectIds.Count);
            }

            if (context.EffectiveReadOnly && IsMutation(httpContext.Request.Method) && !IsSessionEndpoint(httpContext.Request.Path))
            {
                logger.LogInformation("Blocked mutation in read-only management perspective. UserId={UserId} TenantId={TenantId} Perspective={Perspective} PortfolioId={PortfolioId} ProgramId={ProgramId} ProjectId={ProjectId}", context.UserId, context.TenantId, context.SelectedPerspective, context.SelectedPortfolioId, context.SelectedProgramId, context.SelectedProjectId);
                await ForbiddenAsync(httpContext, ReadOnlyMessage);
                return;
            }

            var isAddressableResourceInScope = await IsAddressableResourceInScopeAsync(httpContext, resolver, userId, tenantId, context);
            if (!isAddressableResourceInScope)
            {
                if (isProjectShellDiagnostic)
                {
                    var addressedProjectId = ProjectRequestContext.GetInternalProjectId(httpContext);
                    var addressedPublicId = ProjectRequestContext.GetPublicProjectId(httpContext);
                    var skipProjectManagementScope = ProjectRequestContext.ShouldSkipManagementScope(httpContext);
                    var projectIsInManagementScope = addressedProjectId.HasValue && context.Scope.ProjectIds.Contains(addressedProjectId.Value);
                    logger.LogWarning(
                        "Project Shell request rejected by management-scope enforcement. RequestId={RequestId} UserId={UserId} TenantId={TenantId} PublicProjectId={PublicProjectId} InternalProjectId={InternalProjectId} ProjectFound={ProjectFound} ManagementScopeContainsProject={ManagementScopeContainsProject} SkipManagementScope={SkipManagementScope} EffectivePerspective={EffectivePerspective} EffectiveViewMode={EffectiveViewMode} RejectionReason={RejectionReason}",
                        httpContext.TraceIdentifier,
                        userId,
                        tenantId,
                        addressedPublicId,
                        addressedProjectId,
                        addressedProjectId.HasValue,
                        projectIsInManagementScope,
                        skipProjectManagementScope,
                        context.SelectedPerspective.ToString(),
                        context.ViewMode.ToString(),
                        !addressedProjectId.HasValue
                            ? "ProjectPublicIdRoutingDidNotResolveInternalProjectId"
                            : skipProjectManagementScope
                                ? "ProjectScopeExceptionDidNotAllowRequest"
                                : "ProjectNotInEffectiveManagementScope");
                }

                await NotFoundAsync(httpContext);
                return;
            }
        }

        await _next(httpContext);
    }

    private static async Task<bool> IsAddressableResourceInScopeAsync(HttpContext httpContext, IManagementScopeResolver resolver, Guid userId, Guid tenantId, ManagementContext context)
    {
        // The recovery controller applies its own recovery permissions, authenticated-tenant
        // filtering, and PublicId resolution. This narrowly scoped path exception is only for
        // native mode; it does not grant access to normal entity routes or ReadOnlyViewAs writes.
        if (context.ViewMode == ManagementViewMode.Native
            && IsAssignmentRecoveryPath(httpContext.Request.Path)
            && httpContext.User.HasClaim("permission", AssignmentRecoveryPermissions.View))
            return true;

        if (context.CanReadAllTenantPortfolioProgramData
            && HttpMethods.IsGet(httpContext.Request.Method)
            && IsTenantAdminPortfolioProgramReadPath(httpContext.Request.Path))
            return true;

        if (context.HasTenantAdministratorPortfolioWriteScope
            && HttpMethods.IsPut(httpContext.Request.Method)
            && IsTenantAdminPortfolioUpdatePath(httpContext.Request.Path))
            return true;

        // Self-scoped handover approvals are addressable without a management assignment:
        // the ManagerHandoverService enforces Handover.Approve, tenant isolation, and
        // request.NewManagerId == current user before returning anything.
        if (IsSelfScopedHandoverPendingPath(httpContext.Request.Path))
            return true;

        // Only callers with the explicit administrative initiation permission may
        // address an entity outside their manager assignment scope. Read-only view-as
        // was rejected above; the service still verifies tenant and effective access.
        if (context.ViewMode == ManagementViewMode.Native
            && httpContext.User.HasClaim("permission", "Handover.AdminInitiate")
            && IsAdminHandoverInitiationPath(httpContext.Request.Method, httpContext.Request.Path))
            return true;

        if (ProjectRequestContext.GetInternalProjectId(httpContext) is { } addressedProjectId)
        {
            return ProjectRequestContext.ShouldSkipManagementScope(httpContext)
                || context.Scope.ProjectIds.Contains(addressedProjectId);
        }

        var segments = httpContext.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (!Guid.TryParse(segments[i + 1], out var id)) continue;
            if (segments[i].Equals("portfolios", StringComparison.OrdinalIgnoreCase)) return context.Scope.PortfolioIds.Contains(id);
            if (segments[i].Equals("programs", StringComparison.OrdinalIgnoreCase)) return context.Scope.ProgramIds.Contains(id);
            if (segments[i].Equals("projects", StringComparison.OrdinalIgnoreCase)) return context.Scope.ProjectIds.Contains(id);
        }
        return true;
    }

    private static bool IsProjectShellRequest(PathString path) =>
        path.StartsWithSegments("/api/projects", StringComparison.OrdinalIgnoreCase)
        && path.Value?.EndsWith("/shell", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsMutation(string method) => HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
    private static bool IsAssignmentRecoveryPath(PathString path) => path.StartsWithSegments("/api/governance/assignment-recovery");
    private static bool IsSessionEndpoint(PathString path) => path.StartsWithSegments("/api/auth");
    private static bool IsTimerEndpoint(PathString path) => path.StartsWithSegments("/api/timer");
    private static bool IsTenantAdminPortfolioProgramReadPath(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
        var isApiPath = segments.Length > 0 && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase);
        if (!isApiPath) return false;

        if (segments.Length == 5
            && segments[1].Equals("manager-handovers", StringComparison.OrdinalIgnoreCase)
            && (segments[2].Equals("portfolios", StringComparison.OrdinalIgnoreCase)
                || segments[2].Equals("programs", StringComparison.OrdinalIgnoreCase))
            && Guid.TryParse(segments[3], out _)
            && (segments[4].Equals("requests", StringComparison.OrdinalIgnoreCase)
                || segments[4].Equals("history", StringComparison.OrdinalIgnoreCase)
                || segments[4].Equals("snapshots", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (segments.Length == 2)
            return segments[1].Equals("portfolios", StringComparison.OrdinalIgnoreCase)
                || segments[1].Equals("programs", StringComparison.OrdinalIgnoreCase);

        if (segments.Length == 3 && Guid.TryParse(segments[2], out _))
            return segments[1].Equals("portfolios", StringComparison.OrdinalIgnoreCase)
                || segments[1].Equals("programs", StringComparison.OrdinalIgnoreCase);

        if (segments.Length == 3)
            return (segments[1].Equals("portfolio-management", StringComparison.OrdinalIgnoreCase)
                    && segments[2].Equals("portfolios", StringComparison.OrdinalIgnoreCase))
                || (segments[1].Equals("program-management", StringComparison.OrdinalIgnoreCase)
                    && segments[2].Equals("programs", StringComparison.OrdinalIgnoreCase));

        if (segments.Length != 5
            || !Guid.TryParse(segments[3], out _)
            || !segments[4].Equals("overview", StringComparison.OrdinalIgnoreCase))
            return false;

        return (segments[1].Equals("portfolio-management", StringComparison.OrdinalIgnoreCase)
                && segments[2].Equals("portfolios", StringComparison.OrdinalIgnoreCase))
            || (segments[1].Equals("program-management", StringComparison.OrdinalIgnoreCase)
                && segments[2].Equals("programs", StringComparison.OrdinalIgnoreCase));
    }
    private static bool IsTenantAdminPortfolioUpdatePath(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
        return segments.Length == 3
            && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && segments[1].Equals("portfolios", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(segments[2], out _);
    }
    private static bool IsSelfScopedHandoverPendingPath(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
        return segments.Length == 5
            && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && segments[1].Equals("manager-handovers", StringComparison.OrdinalIgnoreCase)
            && (segments[2].Equals("portfolios", StringComparison.OrdinalIgnoreCase)
                || segments[2].Equals("programs", StringComparison.OrdinalIgnoreCase))
            && Guid.TryParse(segments[3], out _)
            && segments[4].Equals("pending-for-me", StringComparison.OrdinalIgnoreCase);
    }
    private static bool IsAdminHandoverInitiationPath(string method, PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
        if (segments.Length == 4
            && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && segments[1].Equals("projects", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(segments[2], out _)
            && HttpMethods.IsGet(method))
            return segments[3].Equals("handover-capabilities", StringComparison.OrdinalIgnoreCase)
                || segments[3].Equals("handover-managers", StringComparison.OrdinalIgnoreCase);

        if (segments.Length == 5
            && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && segments[1].Equals("projects", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(segments[2], out _)
            && segments[3].Equals("handover-requests", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsPost(method))
            return segments[4].Equals("admin", StringComparison.OrdinalIgnoreCase)
                || segments[4].Equals("initial-assignment", StringComparison.OrdinalIgnoreCase);

        if (segments.Length == 5
            && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && segments[1].Equals("manager-handovers", StringComparison.OrdinalIgnoreCase)
            && (segments[2].Equals("programs", StringComparison.OrdinalIgnoreCase)
                || segments[2].Equals("portfolios", StringComparison.OrdinalIgnoreCase))
            && Guid.TryParse(segments[3], out _)
            && HttpMethods.IsGet(method))
            return segments[4].Equals("eligible-managers", StringComparison.OrdinalIgnoreCase)
                || segments[4].Equals("capabilities", StringComparison.OrdinalIgnoreCase);

        if (segments.Length == 5
            && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && segments[1].Equals("manager-handovers", StringComparison.OrdinalIgnoreCase)
            && (segments[2].Equals("programs", StringComparison.OrdinalIgnoreCase)
                || segments[2].Equals("portfolios", StringComparison.OrdinalIgnoreCase))
            && Guid.TryParse(segments[3], out _)
            && HttpMethods.IsPost(method))
            return segments[4].Equals("admin-requests", StringComparison.OrdinalIgnoreCase)
                || segments[4].Equals("initial-assignment-requests", StringComparison.OrdinalIgnoreCase);

        // The handover services additionally require this caller to be the
        // active-tenant requester of the pending request. The opaque request ID
        // does not grant access to other users' or tenants' requests.
        if (segments.Length == 4
            && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && segments[1].Equals("handover-requests", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(segments[2], out _)
            && segments[3].Equals("cancel", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsPost(method))
            return true;

        if (segments.Length == 6
            && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && segments[1].Equals("manager-handovers", StringComparison.OrdinalIgnoreCase)
            && (segments[2].Equals("programs", StringComparison.OrdinalIgnoreCase)
                || segments[2].Equals("portfolios", StringComparison.OrdinalIgnoreCase))
            && segments[3].Equals("requests", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(segments[4], out _)
            && segments[5].Equals("cancel", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsPost(method))
            return true;

        return false;
    }
    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var id) ? id : null;
    private static T? ParseEnum<T>(string? value) where T : struct, Enum => Enum.TryParse<T>(value, true, out var parsed) ? parsed : null;

    private static Task ForbiddenAsync(HttpContext context, string detail) => WriteProblemAsync(context, StatusCodes.Status403Forbidden, detail);
    private static Task NotFoundAsync(HttpContext context) => WriteProblemAsync(context, StatusCodes.Status404NotFound, "The requested resource was not found.");

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = statusCode, Title = statusCode == 403 ? "Forbidden" : "Not Found", Detail = detail });
    }
}
