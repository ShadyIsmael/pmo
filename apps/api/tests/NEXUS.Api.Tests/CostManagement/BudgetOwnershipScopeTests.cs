using NEXUS.Modules.CostManagement.Application;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.CostManagement;

public sealed class BudgetOwnershipScopeTests
{
    [Fact]
    public void PortfolioOnlyBudget_IsRejectedBecauseRollupsAreReadOnly()
    {
        var error = ProjectBudgetOwnershipGuard.ValidateProjectBudgetOwnership(
            projectId: null,
            requestedPortfolioId: Guid.NewGuid(),
            project: null);

        Assert.NotNull(error);
        Assert.Contains("Project is required", error!.Message, StringComparison.Ordinal);
        Assert.Contains("read-only rollups", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectBudget_RejectsPortfolioDifferentFromAuthoritativeProjectParent()
    {
        var projectId = Guid.NewGuid();
        var currentPortfolioId = Guid.NewGuid();
        var project = new ProjectReference(projectId, "Project", PortfolioId: currentPortfolioId);

        var error = ProjectBudgetOwnershipGuard.ValidateProjectBudgetOwnership(
            projectId,
            Guid.NewGuid(),
            project);

        Assert.NotNull(error);
        Assert.Contains("must match the Project's current Portfolio", error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectBudget_AcceptsOmittedOrMatchingPortfolioForDerivation()
    {
        var projectId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var project = new ProjectReference(projectId, "Project", PortfolioId: portfolioId);

        Assert.Null(ProjectBudgetOwnershipGuard.ValidateProjectBudgetOwnership(projectId, null, project));
        Assert.Null(ProjectBudgetOwnershipGuard.ValidateProjectBudgetOwnership(projectId, portfolioId, project));
    }

    [Fact]
    public void PortfolioOnlyBudget_IsNeverProjectOwnedForRevisionWrites()
    {
        Assert.False(ProjectBudgetOwnershipGuard.IsProjectOwned(null));
        Assert.False(ProjectBudgetOwnershipGuard.IsProjectOwned(Guid.Empty));
        Assert.True(ProjectBudgetOwnershipGuard.IsProjectOwned(Guid.NewGuid()));
    }

    [Fact]
    public void FinanceWriteGuard_RejectsReadOnlyViewAsContext()
    {
        var tenantId = Guid.NewGuid();
        var context = ManagementContextFor(tenantId, ManagementViewMode.ReadOnlyViewAs);

        var error = FinancePermissionGuard.RequireWritableManagementContext(context, tenantId);

        Assert.NotNull(error);
        Assert.Contains("read-only", error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FinanceWriteGuard_RequiresTheCurrentTenantAndWritableContext()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var context = ManagementContextFor(tenantId, ManagementViewMode.Native);

        Assert.Null(FinancePermissionGuard.RequireWritableManagementContext(context, tenantId));
        Assert.NotNull(FinancePermissionGuard.RequireWritableManagementContext(context, otherTenantId));
        Assert.NotNull(FinancePermissionGuard.RequireWritableManagementContext(null, tenantId));
    }

    private static ManagementContext ManagementContextFor(Guid tenantId, ManagementViewMode mode)
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var scope = new ManagementScope(
            new HashSet<Guid>(),
            new HashSet<Guid>(),
            new HashSet<Guid> { projectId });
        return new ManagementContext(
            userId,
            tenantId,
            ManagementPerspective.ProjectManager,
            mode,
            null,
            null,
            projectId,
            scope);
    }
}
