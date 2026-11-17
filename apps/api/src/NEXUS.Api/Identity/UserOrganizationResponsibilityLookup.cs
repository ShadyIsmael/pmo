using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;

namespace NEXUS.Api.Identity;

/// <summary>
/// Keeps a user's derived Organization membership while current management or
/// sponsorship responsibilities still point at resources in that Organization.
/// </summary>
public sealed class UserOrganizationResponsibilityLookup : IUserOrganizationResponsibilityLookup
{
    private readonly DbContext _context;

    public UserOrganizationResponsibilityLookup(DbContext context) => _context = context;

    public async Task<IReadOnlyList<Guid>> GetOrganizationsWithActiveResponsibilitiesAsync(
        Guid tenantId,
        Guid userId,
        IEnumerable<Guid> organizationIds,
        CancellationToken cancellationToken = default)
    {
        var requestedOrganizationIds = organizationIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (tenantId == Guid.Empty || userId == Guid.Empty || requestedOrganizationIds.Length == 0)
            return Array.Empty<Guid>();

        var today = DateTime.UtcNow.Date;
        var blocked = new HashSet<Guid>();

        var portfolioAssignmentOrganizations = await _context.Set<PortfolioManagerAssignment>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId
                && assignment.PortfolioManagerId == userId
                && !assignment.IsDeleted
                && assignment.IsCurrent
                && assignment.Portfolio.TenantId == tenantId
                && !assignment.Portfolio.IsDeleted
                && assignment.Portfolio.OrganizationId.HasValue
                && requestedOrganizationIds.Contains(assignment.Portfolio.OrganizationId.Value))
            .Select(assignment => assignment.Portfolio.OrganizationId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        blocked.UnionWith(portfolioAssignmentOrganizations);

        var portfolioResponsibilities = await _context.Set<PortfolioEntity>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(portfolio => portfolio.TenantId == tenantId
                && !portfolio.IsDeleted
                && portfolio.OrganizationId.HasValue
                && requestedOrganizationIds.Contains(portfolio.OrganizationId.Value)
                && (portfolio.OwnerId == userId || portfolio.SponsorId == userId)
                && (!portfolio.EndDate.HasValue || portfolio.EndDate.Value.Date >= today))
            .Select(portfolio => portfolio.OrganizationId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        blocked.UnionWith(portfolioResponsibilities);

        var programAssignmentOrganizations = await _context.Set<ProgramManagerAssignment>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId
                && assignment.ProgramManagerId == userId
                && !assignment.IsDeleted
                && assignment.IsCurrent
                && assignment.Program.TenantId == tenantId
                && !assignment.Program.IsDeleted
                && assignment.Program.Portfolio.TenantId == tenantId
                && !assignment.Program.Portfolio.IsDeleted
                && assignment.Program.Portfolio.OrganizationId.HasValue
                && requestedOrganizationIds.Contains(assignment.Program.Portfolio.OrganizationId.Value))
            .Select(assignment => assignment.Program.Portfolio.OrganizationId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        blocked.UnionWith(programAssignmentOrganizations);

        var programResponsibilities = await (
                from program in _context.Set<ProgramEntity>().IgnoreQueryFilters().AsNoTracking()
                join portfolio in _context.Set<PortfolioEntity>().IgnoreQueryFilters().AsNoTracking()
                    on new { program.TenantId, Id = program.PortfolioId }
                    equals new { portfolio.TenantId, portfolio.Id }
                where program.TenantId == tenantId
                    && !program.IsDeleted
                    && program.ProgramManagerId == userId
                    && (!program.EndDate.HasValue || program.EndDate.Value.Date >= today)
                    && !portfolio.IsDeleted
                    && portfolio.OrganizationId.HasValue
                    && requestedOrganizationIds.Contains(portfolio.OrganizationId.Value)
                select portfolio.OrganizationId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        blocked.UnionWith(programResponsibilities);

        var projectManagerAssignments = await _context.Set<ProjectManagerAssignment>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId
                && assignment.ProjectManagerId == userId
                && !assignment.IsDeleted
                && assignment.IsCurrent
                && assignment.Project != null
                && assignment.Project.TenantId == tenantId
                && !assignment.Project.IsDeleted
                && !assignment.Project.ActualEndDate.HasValue)
            .Select(assignment => assignment.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var directProjectResponsibilities = await _context.Set<Project>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(project => project.TenantId == tenantId
                && !project.IsDeleted
                && !project.ActualEndDate.HasValue
                && (project.ProjectManagerId == userId || project.SponsorId == userId))
            .Select(project => project.Id)
            .ToListAsync(cancellationToken);

        var projectIds = projectManagerAssignments.Concat(directProjectResponsibilities).Distinct().ToArray();
        if (projectIds.Length > 0)
        {
            var directProjectOrganizations = await (
                    from project in _context.Set<Project>().IgnoreQueryFilters().AsNoTracking()
                    join portfolio in _context.Set<PortfolioEntity>().IgnoreQueryFilters().AsNoTracking()
                        on new { project.TenantId, Id = project.PortfolioId }
                        equals new { portfolio.TenantId, Id = (Guid?)portfolio.Id }
                    where project.TenantId == tenantId
                        && projectIds.Contains(project.Id)
                        && !project.IsDeleted
                        && !portfolio.IsDeleted
                        && portfolio.OrganizationId.HasValue
                        && requestedOrganizationIds.Contains(portfolio.OrganizationId.Value)
                    select portfolio.OrganizationId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
            blocked.UnionWith(directProjectOrganizations);

            var programProjectOrganizations = await (
                    from project in _context.Set<Project>().IgnoreQueryFilters().AsNoTracking()
                    join program in _context.Set<ProgramEntity>().IgnoreQueryFilters().AsNoTracking()
                        on new { project.TenantId, Id = project.ProgramId }
                        equals new { program.TenantId, Id = (Guid?)program.Id }
                    join portfolio in _context.Set<PortfolioEntity>().IgnoreQueryFilters().AsNoTracking()
                        on new { program.TenantId, Id = program.PortfolioId }
                        equals new { portfolio.TenantId, portfolio.Id }
                    where project.TenantId == tenantId
                        && projectIds.Contains(project.Id)
                        && !project.IsDeleted
                        && !program.IsDeleted
                        && !portfolio.IsDeleted
                        && portfolio.OrganizationId.HasValue
                        && requestedOrganizationIds.Contains(portfolio.OrganizationId.Value)
                    select portfolio.OrganizationId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
            blocked.UnionWith(programProjectOrganizations);
        }

        return blocked.ToArray();
    }
}
