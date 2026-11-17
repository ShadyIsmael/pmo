using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using PortfolioProgram = NEXUS.Modules.Portfolio.Domain.Program;

namespace NEXUS.Api.Management;

/// <summary>
/// Restores Organization-owned view permissions for explicitly marked collection
/// endpoints whose services filter every returned row through ManagementScope.
/// The claims exist only for the current GET request and are never issued as JWT
/// claims or added to resource and mutation requests.
/// </summary>
public sealed class OrganizationScopedCollectionPermissionMiddleware
{
    private const string PermissionClaimType = "permission";
    private readonly RequestDelegate _next;

    public OrganizationScopedCollectionPermissionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext httpContext,
        AppDbContext db,
        IManagementContextAccessor managementContextAccessor,
        IUserEffectivePermissionService effectivePermissions,
        IProjectOrganizationLookup projectOrganizationLookup)
    {
        var requirement = httpContext.GetEndpoint()?.Metadata
            .GetMetadata<OrganizationScopedCollectionPermissionAttribute>();
        var managementContext = managementContextAccessor.Current;
        var identity = httpContext.User.Identities.FirstOrDefault(item => item.IsAuthenticated);

        if (!HttpMethods.IsGet(httpContext.Request.Method)
            || requirement is null
            || managementContext is null
            || managementContext.ViewMode != ManagementViewMode.Native
            || identity is null
            || !Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || userId != managementContext.UserId
            || !Guid.TryParse(httpContext.User.FindFirstValue("tenantId"), out var tenantId)
            || tenantId != managementContext.TenantId)
        {
            await _next(httpContext);
            return;
        }

        var requestedOrganizationId = httpContext.Items.TryGetValue(UserPermissionClaimsMiddleware.OrganizationScopeItemKey, out var rawOrganizationId)
            && rawOrganizationId is Guid scopedOrganizationId
                ? scopedOrganizationId
                : (Guid?)null;

        // A complete claim set created before management context selection is
        // tenant-scoped. Organization-derived claims are request-local and carry
        // an OrganizationScopeItemKey, so they still take the filtered path below.
        if (!requestedOrganizationId.HasValue
            && requirement.PermissionKeys.All(key => httpContext.User.HasClaim(PermissionClaimType, key)))
        {
            await _next(httpContext);
            return;
        }

        var qualifyingOrganizationIds = new HashSet<Guid>();
        foreach (var organizationId in managementContext.Scope.OrganizationIds
                     .Where(id => id != Guid.Empty && (!requestedOrganizationId.HasValue || id == requestedOrganizationId.Value))
                     .OrderBy(id => id))
        {
            var organizationIsActive = await db.Set<Organization>().IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(organization => organization.Id == organizationId
                    && organization.TenantId == managementContext.TenantId
                    && organization.IsActive && !organization.IsDeleted,
                    httpContext.RequestAborted);
            if (!organizationIsActive)
                continue;

            var permissions = await effectivePermissions.GetEffectivePermissionsForOrganizationAsync(
                managementContext.UserId,
                managementContext.TenantId,
                organizationId,
                httpContext.RequestAborted);

            if (!requirement.PermissionKeys.All(permissions.Contains))
                continue;

            qualifyingOrganizationIds.Add(organizationId);
        }

        var narrowedContext = await RestrictScopeToOrganizationsAsync(
            db,
            managementContext,
            qualifyingOrganizationIds,
            projectOrganizationLookup,
            httpContext.RequestAborted);
        managementContextAccessor.Set(narrowedContext with
        {
            CanReadAllTenantPortfolioProgramData = false,
            HasTenantAdministratorPortfolioWriteScope = false
        });

        if (qualifyingOrganizationIds.Count > 0)
        {
            foreach (var permissionKey in requirement.PermissionKeys)
                if (!httpContext.User.HasClaim(PermissionClaimType, permissionKey))
                    identity.AddClaim(new Claim(PermissionClaimType, permissionKey));
        }

        await _next(httpContext);
    }

    private static async Task<ManagementContext> RestrictScopeToOrganizationsAsync(
        AppDbContext db,
        ManagementContext managementContext,
        IReadOnlySet<Guid> organizationIds,
        IProjectOrganizationLookup projectOrganizationLookup,
        CancellationToken cancellationToken)
    {
        var tenantId = managementContext.TenantId;
        var scope = managementContext.Scope;
        if (organizationIds.Count == 0)
            return managementContext with
            {
                Scope = ManagementScope.Empty,
                SelectedPortfolioId = null,
                SelectedProgramId = null,
                SelectedProjectId = null
            };

        var portfolioIds = scope.PortfolioIds.Count == 0 || organizationIds.Count == 0
            ? new HashSet<Guid>()
            : await db.Set<Portfolio>().IgnoreQueryFilters().AsNoTracking()
                .Where(portfolio => portfolio.TenantId == tenantId && !portfolio.IsDeleted
                    && portfolio.OrganizationId.HasValue && organizationIds.Contains(portfolio.OrganizationId.Value)
                    && scope.PortfolioIds.Contains(portfolio.Id))
                .Select(portfolio => portfolio.Id)
                .ToHashSetAsync(cancellationToken);

        var programIds = scope.ProgramIds.Count == 0 || organizationIds.Count == 0
            ? new HashSet<Guid>()
            : await (from program in db.Set<PortfolioProgram>().IgnoreQueryFilters().AsNoTracking()
                     join portfolio in db.Set<Portfolio>().IgnoreQueryFilters().AsNoTracking()
                         on new { program.TenantId, PortfolioId = program.PortfolioId }
                         equals new { portfolio.TenantId, PortfolioId = portfolio.Id }
                     where program.TenantId == tenantId && !program.IsDeleted && !portfolio.IsDeleted
                         && portfolio.OrganizationId.HasValue && organizationIds.Contains(portfolio.OrganizationId.Value)
                         && scope.ProgramIds.Contains(program.Id)
                     select program.Id)
                .ToHashSetAsync(cancellationToken);

        var projectOrganizationIds = new Dictionary<Guid, Guid>();
        foreach (var projectId in scope.ProjectIds.OrderBy(id => id))
        {
            var organizationId = await projectOrganizationLookup.ResolveOrganizationIdAsync(
                tenantId, projectId, cancellationToken);
            if (organizationId.HasValue && organizationIds.Contains(organizationId.Value))
                projectOrganizationIds[projectId] = organizationId.Value;
        }
        var projectIds = projectOrganizationIds.Keys.ToHashSet();
        var selectedProjectId = managementContext.SelectedProjectId is Guid selectedProject
            && projectIds.Contains(selectedProject) ? selectedProject : (Guid?)null;
        var selectedProgramId = managementContext.SelectedProgramId is Guid selectedProgram
            && programIds.Contains(selectedProgram) ? selectedProgram : (Guid?)null;
        var selectedPortfolioId = managementContext.SelectedPortfolioId is Guid selectedPortfolio
            && portfolioIds.Contains(selectedPortfolio) ? selectedPortfolio : (Guid?)null;

        var narrowedScope = new ManagementScope(portfolioIds, programIds, projectIds)
        {
            OrganizationIds = organizationIds.ToHashSet(),
            OrganizationReferences = scope.OrganizationReferences
                .Where(reference => organizationIds.Contains(reference.Id))
                .ToArray(),
            ProjectOrganizationIds = projectOrganizationIds
        };
        return managementContext with
        {
            Scope = narrowedScope,
            SelectedPortfolioId = selectedPortfolioId,
            SelectedProgramId = selectedProgramId,
            SelectedProjectId = selectedProjectId
        };
    }
}
