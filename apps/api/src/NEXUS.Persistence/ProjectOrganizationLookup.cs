using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;
using PortfolioEntity = NEXUS.Modules.Portfolio.Domain.Portfolio;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;

namespace NEXUS.Persistence;

/// <summary>Resolves an active Organization from the canonical Project ownership graph.</summary>
public sealed class ProjectOrganizationLookup(AppDbContext context) : IProjectOrganizationLookup
{
    public async Task<Guid?> ResolveOrganizationIdAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty) return null;

        var project = await context.Projects.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == projectId && item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => new { item.ProgramId, item.PortfolioId, item.DepartmentId })
            .SingleOrDefaultAsync(cancellationToken);
        if (project is null) return null;

        Guid? portfolioId = project.PortfolioId;
        if (project.ProgramId.HasValue)
        {
            portfolioId = await context.Programs.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == project.ProgramId.Value && item.TenantId == tenantId && !item.IsDeleted)
                .Select(item => (Guid?)item.PortfolioId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        Guid? organizationId = null;
        if (portfolioId.HasValue)
        {
            organizationId = await context.Portfolios.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == portfolioId.Value && item.TenantId == tenantId && !item.IsDeleted)
                .Select(item => item.OrganizationId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        if (!organizationId.HasValue && project.DepartmentId.HasValue)
        {
            organizationId = await (
                from department in context.Departments.IgnoreQueryFilters().AsNoTracking()
                join businessUnit in context.BusinessUnits.IgnoreQueryFilters().AsNoTracking()
                    on new { department.TenantId, department.BusinessUnitId }
                    equals new { businessUnit.TenantId, BusinessUnitId = businessUnit.Id }
                where department.Id == project.DepartmentId.Value
                    && department.TenantId == tenantId && !department.IsDeleted && department.IsActive
                    && !businessUnit.IsDeleted && businessUnit.IsActive
                select (Guid?)businessUnit.OrganizationId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        if (!organizationId.HasValue) return null;
        return await context.Organizations.IgnoreQueryFilters().AsNoTracking()
            .Where(organization => organization.Id == organizationId.Value && organization.TenantId == tenantId
                && !organization.IsDeleted && organization.IsActive)
            .Select(organization => (Guid?)organization.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
