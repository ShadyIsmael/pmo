using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
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

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var routeParameter = GetProjectRouteParameter(context);
        var hasRouteProjectId = routeParameter is not null;
        var hasQueryProjectId = context.Request.Query.ContainsKey("projectId");
        if (!hasRouteProjectId && !hasQueryProjectId)
        {
            await _next(context);
            return;
        }

        if (!Guid.TryParse(context.User.FindFirst("tenantId")?.Value, out var tenantId))
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "A valid tenant context is required.");
            return;
        }

        Guid? externalRouteId = null;
        if (hasRouteProjectId)
        {
            if (!Guid.TryParse(Convert.ToString(context.Request.RouteValues[routeParameter!]), out var parsedRouteId))
            {
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

        if (externalRouteId.HasValue && externalQueryId.HasValue && externalRouteId.Value != externalQueryId.Value)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "The requested resource was not found.");
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
        await _next(context);
    }

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
