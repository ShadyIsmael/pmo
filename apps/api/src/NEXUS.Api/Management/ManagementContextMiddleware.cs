using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
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

    public async Task InvokeAsync(HttpContext httpContext, IManagementScopeResolver resolver, IManagementContextAccessor accessor, ILogger<ManagementContextMiddleware> logger)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            await _next(httpContext);
            return;
        }

        if (!Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || !Guid.TryParse(httpContext.User.FindFirstValue("tenantId"), out var tenantId))
        {
            await _next(httpContext);
            return;
        }

        var rawPerspective = httpContext.Request.Headers[PerspectiveHeader].FirstOrDefault();
        var rawViewMode = httpContext.Request.Headers[ViewModeHeader].FirstOrDefault();
        var perspective = ParseEnum<ManagementPerspective>(rawPerspective);
        var parsedViewMode = ParseEnum<ManagementViewMode>(rawViewMode);
        if ((!string.IsNullOrWhiteSpace(rawPerspective) && !perspective.HasValue) || (!string.IsNullOrWhiteSpace(rawViewMode) && !parsedViewMode.HasValue))
        {
            await ForbiddenAsync(httpContext, "The requested perspective or context is not available.");
            return;
        }
        var viewMode = parsedViewMode ?? ManagementViewMode.Native;
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
        if (context is null && (perspective.HasValue || viewMode == ManagementViewMode.ReadOnlyViewAs) && !hasTenantPortfolioAdminScope)
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

            await ForbiddenAsync(httpContext, "The requested perspective or context is not available.");
            return;
        }

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
            if (context.EffectiveReadOnly && IsMutation(httpContext.Request.Method) && !IsSessionEndpoint(httpContext.Request.Path))
            {
                logger.LogInformation("Blocked mutation in read-only management perspective. UserId={UserId} TenantId={TenantId} Perspective={Perspective} PortfolioId={PortfolioId} ProgramId={ProgramId} ProjectId={ProjectId}", context.UserId, context.TenantId, context.SelectedPerspective, context.SelectedPortfolioId, context.SelectedProgramId, context.SelectedProjectId);
                await ForbiddenAsync(httpContext, ReadOnlyMessage);
                return;
            }

            if (!await IsAddressableResourceInScopeAsync(httpContext, resolver, userId, tenantId, context))
            {
                await NotFoundAsync(httpContext);
                return;
            }
        }

        await _next(httpContext);
    }

    private static async Task<bool> IsAddressableResourceInScopeAsync(HttpContext httpContext, IManagementScopeResolver resolver, Guid userId, Guid tenantId, ManagementContext context)
    {
        if (context.CanReadAllTenantPortfolioProgramData
            && HttpMethods.IsGet(httpContext.Request.Method)
            && IsTenantAdminPortfolioProgramReadPath(httpContext.Request.Path))
            return true;

        if (context.HasTenantAdministratorPortfolioWriteScope
            && HttpMethods.IsPut(httpContext.Request.Method)
            && IsTenantAdminPortfolioUpdatePath(httpContext.Request.Path))
            return true;

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

    private static bool IsMutation(string method) => HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
    private static bool IsSessionEndpoint(PathString path) => path.StartsWithSegments("/api/auth");
    private static bool IsTimerEndpoint(PathString path) => path.StartsWithSegments("/api/timer");
    private static bool IsTenantAdminPortfolioProgramReadPath(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
        var isApiPath = segments.Length > 0 && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase);
        if (!isApiPath) return false;

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
