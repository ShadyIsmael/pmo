using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;

namespace NEXUS.Api.Management;

/// <summary>
/// Resolves public project identifiers at the HTTP boundary. Internal project IDs
/// remain for database relationships and application services, but are never
/// accepted as external project route/query identifiers.
/// </summary>
public sealed class ProjectPublicIdRoutingMiddleware
{
    private readonly RequestDelegate _next;

    public ProjectPublicIdRoutingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppDbContext db, ILogger<ProjectPublicIdRoutingMiddleware> logger, IWebHostEnvironment environment)
    {
        var isProjectShellDiagnostic = environment.IsDevelopment() && IsProjectShellRequest(context.Request.Path);
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenantIdClaim = context.User.FindFirst("tenantId")?.Value;
        var userIdResolved = Guid.TryParse(userIdClaim, out var resolvedUserId) && resolvedUserId != Guid.Empty;
        var tenantIdResolved = Guid.TryParse(tenantIdClaim, out var resolvedTenantId) && resolvedTenantId != Guid.Empty;
        var hasPerspectiveHeader = context.Request.Headers.ContainsKey(ManagementContextMiddleware.PerspectiveHeader);
        var hasViewModeHeader = context.Request.Headers.ContainsKey(ManagementContextMiddleware.ViewModeHeader);
        var hasProjectHeader = context.Request.Headers.ContainsKey(ManagementContextMiddleware.ProjectHeader);

        if (isProjectShellDiagnostic)
        {
            logger.LogInformation(
                "Project Shell PublicId routing started. RequestId={RequestId} Authenticated={Authenticated} UserIdResolved={UserIdResolved} UserId={UserId} TenantIdResolved={TenantIdResolved} TenantId={TenantId} PerspectiveHeaderPresent={PerspectiveHeaderPresent} ViewModeHeaderPresent={ViewModeHeaderPresent} ProjectIdHeaderPresent={ProjectIdHeaderPresent}",
                context.TraceIdentifier,
                context.User.Identity?.IsAuthenticated == true,
                userIdResolved,
                userIdResolved ? resolvedUserId : null,
                tenantIdResolved,
                tenantIdResolved ? resolvedTenantId : null,
                hasPerspectiveHeader,
                hasViewModeHeader,
                hasProjectHeader);
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            if (isProjectShellDiagnostic)
            {
                logger.LogInformation(
                    "Project Shell PublicId routing bypassed for an unauthenticated request. RequestId={RequestId} RejectionReason={RejectionReason}",
                    context.TraceIdentifier,
                    "UnauthenticatedRequestContinuesToEndpointAuthorization");
            }

            await _next(context);
            return;
        }

        var routeParameter = GetProjectRouteParameter(context);
        var hasRouteProjectId = routeParameter is not null;
        var hasQueryProjectId = context.Request.Query.ContainsKey("projectId");
        var hasQueryProjectIds = context.Request.Query.ContainsKey("projectIds");
        if (!hasRouteProjectId && !hasQueryProjectId && !hasQueryProjectIds)
        {
            if (isProjectShellDiagnostic)
            {
                logger.LogWarning(
                    "Project Shell route was not resolved to a project identifier by PublicId routing. RequestId={RequestId} RejectionReason={RejectionReason}",
                    context.TraceIdentifier,
                    "ProjectRouteParameterNotDetected");
            }

            await _next(context);
            return;
        }

        if (hasQueryProjectId && hasQueryProjectIds)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Provide either projectId or projectIds, not both.");
            return;
        }

        if (!Guid.TryParse(context.User.FindFirst("tenantId")?.Value, out var tenantId))
        {
            if (isProjectShellDiagnostic)
            {
                logger.LogWarning(
                    "Project Shell PublicId routing rejected the request. RequestId={RequestId} RejectionReason={RejectionReason}",
                    context.TraceIdentifier,
                    "TenantIdClaimMissingOrInvalid");
            }

            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "A valid tenant context is required.");
            return;
        }

        Guid? externalRouteId = null;
        if (hasRouteProjectId)
        {
            if (!Guid.TryParse(Convert.ToString(context.Request.RouteValues[routeParameter!]), out var parsedRouteId))
            {
                if (isProjectShellDiagnostic)
                {
                    logger.LogWarning(
                        "Project Shell PublicId routing rejected the route identifier. RequestId={RequestId} RouteParameter={RouteParameter} RejectionReason={RejectionReason}",
                        context.TraceIdentifier,
                        routeParameter,
                        "RouteProjectIdentifierInvalid");
                }

                await WriteProblemAsync(context, StatusCodes.Status404NotFound, "The requested resource was not found.");
                return;
            }
            externalRouteId = parsedRouteId;
        }

        Guid? externalQueryId = null;
        if (hasQueryProjectId)
        {
            var values = context.Request.Query["projectId"];
            if (values.Count != 1 || !Guid.TryParse(values[0], out var parsedQueryId))
            {
                await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "A single valid project identifier is required.");
                return;
            }
            externalQueryId = parsedQueryId;
        }

        var externalQueryIds = Array.Empty<Guid>();
        if (hasQueryProjectIds)
        {
            var parsedIds = new List<Guid>();
            foreach (var rawValue in context.Request.Query["projectIds"])
            {
                foreach (var part in (rawValue ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!Guid.TryParse(part, out var parsedId) || parsedId == Guid.Empty)
                    {
                        await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "projectIds must contain valid project identifiers.");
                        return;
                    }
                    if (!parsedIds.Contains(parsedId)) parsedIds.Add(parsedId);
                }
            }
            if (parsedIds.Count == 0)
            {
                await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "At least one valid project identifier is required.");
                return;
            }
            externalQueryIds = parsedIds.ToArray();
        }

        if (externalRouteId.HasValue && externalQueryId.HasValue && externalRouteId.Value != externalQueryId.Value)
        {
            if (isProjectShellDiagnostic)
            {
                logger.LogWarning(
                    "Project Shell PublicId routing rejected conflicting route and query identifiers. RequestId={RequestId} TenantId={TenantId} RejectionReason={RejectionReason}",
                    context.TraceIdentifier,
                    tenantId,
                    "RouteAndQueryProjectIdentifiersConflict");
            }

            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "The requested resource was not found.");
            return;
        }
        if (externalRouteId.HasValue && externalQueryIds.Length > 0 && !externalQueryIds.Contains(externalRouteId.Value))
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "The requested resource was not found.");
            return;
        }

        if (externalQueryIds.Length > 0)
        {
            var projects = await db.Projects
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && externalQueryIds.Contains(item.PublicId))
                .Select(item => new { item.Id, item.PublicId })
                .ToListAsync(context.RequestAborted);

            if (projects.Count != externalQueryIds.Length)
            {
                await WriteProblemAsync(context, StatusCodes.Status404NotFound, "The requested resource was not found.");
                return;
            }

            var internalIds = string.Join(',', projects.Select(item => item.Id.ToString("D")));
            var query = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in context.Request.Query)
                query[pair.Key] = pair.Key.Equals("projectIds", StringComparison.OrdinalIgnoreCase)
                    ? new StringValues(internalIds)
                    : pair.Value;

            var queryFeature = context.Features.Get<IQueryFeature>();
            if (queryFeature is null)
            {
                await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "The project filter could not be resolved.");
                return;
            }
            queryFeature.Query = new QueryCollection(query);
            await _next(context);
            return;
        }

        var publicId = externalRouteId ?? externalQueryId!.Value;
        var project = await db.Projects
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PublicId == publicId && !item.IsDeleted)
            .Select(item => new { item.Id, item.PublicId })
            .SingleOrDefaultAsync(context.RequestAborted);

        // Resolve by PublicId only. Never fall back to Project.Id. Checking only
        // this key also avoids rejecting a valid PublicId due to an accidental
        // collision with another row's internal ID.
        if (project is null)
        {
            if (isProjectShellDiagnostic)
            {
                logger.LogWarning(
                    "Project Shell PublicId lookup rejected the request. RequestId={RequestId} TenantId={TenantId} PublicId={PublicId} ProjectFound={ProjectFound} RejectionReason={RejectionReason}",
                    context.TraceIdentifier,
                    tenantId,
                    publicId,
                    false,
                    "NoActiveProjectWithPublicIdInAuthenticatedTenant");
            }

            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "The requested resource was not found.");
            return;
        }

        if (routeParameter is not null)
            context.Request.RouteValues[routeParameter] = project.Id;

        if (hasQueryProjectId)
        {
            var query = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in context.Request.Query)
                query[pair.Key] = pair.Key.Equals("projectId", StringComparison.OrdinalIgnoreCase)
                    ? new StringValues(project.Id.ToString("D"))
                    : pair.Value;

            var queryFeature = context.Features.Get<IQueryFeature>();
            if (queryFeature is null)
            {
                await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "The project filter could not be resolved.");
                return;
            }
            queryFeature.Query = new QueryCollection(query);
        }

        context.Items[ProjectRequestContext.InternalProjectIdKey] = project.Id;
        context.Items[ProjectRequestContext.PublicProjectIdKey] = project.PublicId;
        context.Items[ProjectRequestContext.SkipManagementScopeKey] = IsStakeholderProjectRoute(context.Request.Path);

        if (isProjectShellDiagnostic)
        {
            logger.LogInformation(
                "Project Shell PublicId lookup resolved. RequestId={RequestId} TenantId={TenantId} PublicId={PublicId} InternalProjectId={InternalProjectId} ProjectFound={ProjectFound} RouteParameter={RouteParameter}",
                context.TraceIdentifier,
                tenantId,
                project.PublicId,
                project.Id,
                true,
                routeParameter);
        }

        await _next(context);
    }

    private static bool IsProjectShellRequest(PathString path) =>
        path.StartsWithSegments("/api/projects", StringComparison.OrdinalIgnoreCase)
        && path.Value?.EndsWith("/shell", StringComparison.OrdinalIgnoreCase) == true;

    private static string? GetProjectRouteParameter(HttpContext context)
    {
        if (context.Request.RouteValues.Keys.FirstOrDefault(key => key.Equals("projectId", StringComparison.OrdinalIgnoreCase)) is { } projectParameter)
            return projectParameter;

        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (action?.ControllerName.Equals("Project", StringComparison.OrdinalIgnoreCase) == true
            && context.Request.RouteValues.ContainsKey("id"))
            return "id";

        return null;
    }

    private static bool IsStakeholderProjectRoute(PathString path) => path.StartsWithSegments("/api/stakeholder/projects", StringComparison.OrdinalIgnoreCase);

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status401Unauthorized ? "Unauthorized" : statusCode == StatusCodes.Status400BadRequest ? "Bad Request" : "Not Found",
            Detail = detail
        }, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}

public static class ProjectRequestContext
{
    public const string InternalProjectIdKey = "NEXUS.InternalProjectId";
    public const string PublicProjectIdKey = "NEXUS.PublicProjectId";
    public const string SkipManagementScopeKey = "NEXUS.SkipProjectManagementScope";

    public static Guid? GetInternalProjectId(HttpContext context) =>
        context.Items.TryGetValue(InternalProjectIdKey, out var value) && value is Guid id ? id : null;

    public static Guid? GetPublicProjectId(HttpContext context) =>
        context.Items.TryGetValue(PublicProjectIdKey, out var value) && value is Guid id ? id : null;

    public static bool ShouldSkipManagementScope(HttpContext context) =>
        context.Items.TryGetValue(SkipManagementScopeKey, out var value) && value is true;
}
