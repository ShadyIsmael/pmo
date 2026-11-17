using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NEXUS.Modules.Identity.Application;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Infrastructure;

public sealed class UserPermissionClaimsMiddleware
{
    private const string PermissionClaimType = "permission";
    private const string SessionClaimType = "sid";
    public const string OrganizationScopeItemKey = "Nexus.OrganizationScopeId";
    private readonly RequestDelegate _next;

    public UserPermissionClaimsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        IUserEffectivePermissionService permissions,
        IUserSessionValidator sessions,
        ITenantRegistryStatusValidator tenantRegistry,
        IProjectOrganizationLookup? projectOrganizations = null)
    {
        if (context.User.Identity is ClaimsIdentity identity && identity.IsAuthenticated)
        {
            // Strip authorization snapshots before any downstream middleware can
            // observe them, including the invalid-session response path.
            foreach (var claim in context.User.FindAll(PermissionClaimType).Concat(context.User.FindAll(ClaimTypes.Role)).ToArray())
            {
                if (claim.Subject is ClaimsIdentity subject)
                    subject.RemoveClaim(claim);
            }

            if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            {
                await _next(context);
                return;
            }

            var tenantClaim = context.User.FindFirstValue("tenantId");
            if (HasPlatformAuthority(context.User) && !Guid.TryParse(tenantClaim, out _))
            {
                if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? context.User.FindFirstValue("sub"), out var platformActorId)
                    || platformActorId == Guid.Empty)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                foreach (var claim in context.User.FindAll("tenantId").ToArray())
                    if (claim.Subject is ClaimsIdentity subject) subject.RemoveClaim(claim);
                await _next(context);
                return;
            }

            if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                || !Guid.TryParse(context.User.FindFirstValue("tenantId"), out var tenantId)
                || !Guid.TryParse(context.User.FindFirstValue(SessionClaimType), out var sessionId)
                || !await sessions.IsActiveAsync(sessionId, userId, tenantId, context.RequestAborted))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            if (!await tenantRegistry.IsActiveAsync(tenantId, context.RequestAborted))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            // Never trust the permission snapshot embedded in a long-lived JWT. Rebuild
            // it from the tenant-scoped source of truth on every authenticated request,
            // so direct denials and revocations apply without waiting for token expiry.
            var organizationResolution = await TryResolveRequestedOrganizationIdAsync(
                context, tenantId, projectOrganizations);
            if (!organizationResolution.Success)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            var requestedOrganizationId = organizationResolution.OrganizationId;

            if (requestedOrganizationId is Guid scopedOrganizationId)
                context.Items[OrganizationScopeItemKey] = scopedOrganizationId;
            var currentPermissions = requestedOrganizationId is Guid organizationId
                ? await permissions.GetEffectivePermissionsForOrganizationAsync(userId, tenantId, organizationId, context.RequestAborted)
                : await permissions.GetEffectivePermissionsAsync(userId, tenantId, context.RequestAborted);
            foreach (var permission in currentPermissions)
                identity.AddClaim(new Claim(PermissionClaimType, permission));

            var currentRoles = await permissions.GetEffectiveRoleNamesAsync(userId, tenantId, context.RequestAborted);
            foreach (var role in currentRoles)
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        await _next(context);
    }

    private static bool HasPlatformAuthority(ClaimsPrincipal principal) =>
        principal.FindAll("platform_role").Any(claim =>
            string.Equals(claim.Value, "PlatformAdministrator", StringComparison.OrdinalIgnoreCase))
        || principal.FindAll("platform_permission").Any(claim =>
            claim.Value is "tenants.manage");

    private static async Task<(bool Success, Guid? OrganizationId)> TryResolveRequestedOrganizationIdAsync(
        HttpContext context,
        Guid tenantId,
        IProjectOrganizationLookup? projectOrganizations)
    {
        var routeValue = context.Request.RouteValues.TryGetValue("organizationId", out var routeOrganization)
            ? Convert.ToString(routeOrganization, System.Globalization.CultureInfo.InvariantCulture)
            : null;
        var queryValues = context.Request.Query["organizationId"];
        if (queryValues.Count > 1)
            return (false, null);
        var queryValue = queryValues.Count == 1 ? queryValues[0] : null;

        if (!string.IsNullOrWhiteSpace(routeValue) && !string.IsNullOrWhiteSpace(queryValue)
            && !string.Equals(routeValue, queryValue, StringComparison.OrdinalIgnoreCase))
            return (false, null);

        var rawValue = string.IsNullOrWhiteSpace(routeValue) ? queryValue : routeValue;
        if (!string.IsNullOrWhiteSpace(rawValue)
            && (!Guid.TryParse(rawValue, out var parsedOrganizationId) || parsedOrganizationId == Guid.Empty))
            return (false, null);

        var explicitOrganizationId = string.IsNullOrWhiteSpace(rawValue) ? (Guid?)null : Guid.Parse(rawValue);
        var projectIdCandidates = new List<Guid>();
        if (context.Request.RouteValues.TryGetValue("projectId", out var routeProject)
            && !TryAddProjectId(Convert.ToString(routeProject, System.Globalization.CultureInfo.InvariantCulture), projectIdCandidates))
            return (false, null);

        foreach (var headerName in new[] { "X-Nexus-Project-Id" })
        {
            var values = context.Request.Headers[headerName];
            if (values.Count > 1
                || (values.Count == 1 && !TryAddProjectId(values[0], projectIdCandidates)))
                return (false, null);
        }

        foreach (var queryName in new[] { "projectId", "forProjectId" })
        {
            var values = context.Request.Query[queryName];
            if (values.Count > 1
                || (values.Count == 1 && !TryAddProjectId(values[0], projectIdCandidates)))
                return (false, null);
        }

        var distinctProjectIds = projectIdCandidates.Distinct().ToArray();
        if (distinctProjectIds.Length > 1)
            return (false, null);

        Guid? projectOrganizationId = null;
        if (distinctProjectIds.Length == 1)
        {
            if (projectOrganizations is null)
                return (false, null);
            projectOrganizationId = await projectOrganizations.ResolveOrganizationIdAsync(
                tenantId, distinctProjectIds[0], context.RequestAborted);
            if (!projectOrganizationId.HasValue)
                return (false, null);
        }

        if (explicitOrganizationId.HasValue && projectOrganizationId.HasValue
            && explicitOrganizationId.Value != projectOrganizationId.Value)
            return (false, null);

        return (true, explicitOrganizationId ?? projectOrganizationId);
    }

    private static bool TryAddProjectId(string? rawValue, ICollection<Guid> projectIds)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return true;
        if (!Guid.TryParse(rawValue, out var projectId) || projectId == Guid.Empty)
            return false;
        projectIds.Add(projectId);
        return true;
    }
}
