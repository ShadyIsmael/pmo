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

        return await ResolveOrganizationIdAsync(
            tenantId, project.ProgramId, project.PortfolioId, project.DepartmentId, cancellationToken);
    }

    public async Task<Guid?> ResolveOrganizationIdAsync(
        Guid tenantId,
        Guid? programId,
        Guid? portfolioId,
        Guid? departmentId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) return null;

        var resolvedPortfolioId = portfolioId;
        if (programId.HasValue)
        {
            resolvedPortfolioId = await context.Programs.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == programId.Value && item.TenantId == tenantId && !item.IsDeleted)
                .Select(item => (Guid?)item.PortfolioId)
                .SingleOrDefaultAsync(cancellationToken);
            if (!resolvedPortfolioId.HasValue) return null;
            if (portfolioId.HasValue && portfolioId.Value != resolvedPortfolioId.Value) return null;
        }

        Guid? organizationId = null;
        if (resolvedPortfolioId.HasValue)
        {
            organizationId = await context.Portfolios.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == resolvedPortfolioId.Value && item.TenantId == tenantId && !item.IsDeleted)
                .Select(item => item.OrganizationId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        if (!organizationId.HasValue && departmentId.HasValue)
        {
            organizationId = await (
                from department in context.Departments.IgnoreQueryFilters().AsNoTracking()
                join businessUnit in context.BusinessUnits.IgnoreQueryFilters().AsNoTracking()
                    on new { department.TenantId, department.BusinessUnitId }
                    equals new { businessUnit.TenantId, BusinessUnitId = businessUnit.Id }
                where department.Id == departmentId.Value
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
