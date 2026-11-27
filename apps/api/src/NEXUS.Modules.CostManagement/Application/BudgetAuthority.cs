using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Application;

/// <summary>Budget Lines reserve allocations; they never grant project financial authority.</summary>
internal static class BudgetAuthority
{
    public static async Task<Error?> ValidateAllocationAsync(DbContext context, IProjectReferenceLookup projects,
        Guid? projectId, Guid? excludingBudgetId, decimal proposed, CancellationToken cancellationToken)
    {
        if (!projectId.HasValue) return null;
        var project = await projects.FindAsync(projectId.Value, cancellationToken);
        if (project is null) return Error.NotFound("Project not found");
        // Authoritative project lifetime ceiling is Project.Budget (exposed via the project reference).
        if (!project.CurrentApprovedProjectBudget.HasValue)
            return Error.Validation("Project budget ceiling is not established. Obtain Project Management budget approval before allocating funds.");
        var ceiling = project.CurrentApprovedProjectBudget.Value;
        // ApprovedAmount is the current allocation on every non-deleted line; editable labels are not approval gates.
        var allocated = await context.Set<Budget>()
            .Where(b => b.ProjectId == projectId && !b.IsDeleted && (!excludingBudgetId.HasValue || b.Id != excludingBudgetId))
            .SumAsync(b => (decimal?)b.ApprovedAmount, cancellationToken) ?? 0;
        var excess = allocated + proposed - ceiling;
        var currency = (project.Currency ?? string.Empty).Trim().ToUpperInvariant();
        return excess > 0
            ? Error.Validation($"Budget allocation exceeds the current approved project budget by {currency} {excess:N2}. Submit a Project Change Request to increase the project budget.")
            : null;
    }
}
