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
        if (!project.CurrentApprovedProjectBudget.HasValue)
            return Error.Validation("Project budget ceiling is not established. Obtain Project Management budget approval before allocating funds.");
        // ApprovedAmount is the current allocation on every non-deleted line; editable labels are not approval gates.
        var allocated = await context.Set<Budget>()
            .Where(b => b.ProjectId == projectId && !b.IsDeleted && (!excludingBudgetId.HasValue || b.Id != excludingBudgetId))
            .SumAsync(b => (decimal?)b.ApprovedAmount, cancellationToken) ?? 0;
        var excess = allocated + proposed - project.CurrentApprovedProjectBudget.Value;
        return excess > 0
            ? Error.Validation($"Budget allocation exceeds the current approved project budget by {project.Currency} {excess:N2}. Submit a Project Change Request to increase the project budget.")
            : null;
    }

    public static async Task<Error?> ValidateCommitmentAsync(DbContext context, IProjectReferenceLookup projects,
        Guid projectId, Guid? excludingId, decimal proposed, CancellationToken cancellationToken)
    {
        var project = await projects.FindAsync(projectId, cancellationToken);
        if (project is null) return Error.NotFound("Project not found");
        if (!project.CurrentApprovedProjectBudget.HasValue)
            return Error.Validation("Project budget ceiling is not established. Obtain Project Management budget approval before committing funds.");
        var currency = project.Currency.Trim().ToUpper();
        var actual = await context.Set<CostEntry>()
            .Where(c => c.ProjectId == projectId && !c.IsDeleted && c.Currency.Trim().ToUpper() == currency)
            .SumAsync(c => (decimal?)c.Amount, cancellationToken) ?? 0;
        var committed = await context.Set<PurchaseCommitment>()
            .Where(c => c.ProjectId == projectId && !c.IsDeleted && c.Status != "Closed" && c.Status != "Cancelled"
                && c.Currency.Trim().ToUpper() == currency && (!excludingId.HasValue || c.Id != excludingId))
            .SumAsync(c => (decimal?)c.Amount, cancellationToken) ?? 0;
        var excess = actual + committed + proposed - project.CurrentApprovedProjectBudget.Value;
        return excess > 0
            ? Error.Validation($"Commitment exceeds the current approved project budget by {currency} {excess:N2}. Submit a Project Change Request for budget increase approval.")
            : null;
    }
}
