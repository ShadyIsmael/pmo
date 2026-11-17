using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Application;

internal static class ProjectBudgetOwnershipGuard
{
    internal static bool IsProjectOwned(Guid? projectId) =>
        projectId.HasValue && projectId.Value != Guid.Empty;

    internal static Error? ValidateProjectBudgetOwnership(
        Guid? projectId, Guid? requestedPortfolioId, ProjectReference? project)
    {
        if (!IsProjectOwned(projectId))
            return Error.Validation("A Project is required; Portfolio budgets are read-only rollups.");
        if (project is null || project.Id != projectId!.Value)
            return Error.NotFound("Project not found.");
        if (requestedPortfolioId.HasValue && requestedPortfolioId.Value != project.PortfolioId)
            return Error.Validation("PortfolioId must match the Project's current Portfolio.");
        return null;
    }
}
