using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.Portfolio.Domain;

namespace NEXUS.Persistence;

/// <summary>
/// Prevents a Tenant fiscal calendar from changing after fiscal-year-dependent budget records exist.
/// The query runs through the API's scoped DbContext while the settings repository owns a serializable transaction.
/// </summary>
public sealed class FiscalYearConfigurationChangeGuard : IFiscalYearConfigurationChangeGuard
{
    private readonly DbContext _context;

    public FiscalYearConfigurationChangeGuard(DbContext context) => _context = context;

    public async Task<string?> ValidateChangeAsync(
        Guid tenantId,
        int previousStartMonth,
        int newStartMonth,
        CancellationToken cancellationToken = default)
    {
        if (previousStartMonth == newStartMonth) return null;

        var hasProjectOrTenantBudgetHistory = await _context.Set<Budget>()
            .IgnoreQueryFilters()
            .AnyAsync(budget => budget.TenantId == tenantId, cancellationToken);
        var hasPortfolioBudgetHistory = await _context.Set<PortfolioBudget>()
            .IgnoreQueryFilters()
            .AnyAsync(budget => budget.TenantId == tenantId, cancellationToken);
        // The additive annual-budget migration may not yet be deployed to an existing
        // environment. Until the table exists there cannot be annual-budget history to
        // protect, and a Tenant settings save must not fail with SQL error 208.
        var annualBudgetTableExists = await _context.Database.SqlQueryRaw<int>(
                "SELECT CASE WHEN OBJECT_ID(@tableName, N'U') IS NULL THEN 0 ELSE 1 END AS [Value]",
                new SqlParameter("@tableName", "dbo.ProjectAnnualBudgets"))
            .SingleAsync(cancellationToken) == 1;
        var hasProjectAnnualBudgetHistory = annualBudgetTableExists && await _context.Set<ProjectAnnualBudget>()
            .IgnoreQueryFilters()
            .AnyAsync(budget => budget.TenantId == tenantId, cancellationToken);

        return hasProjectOrTenantBudgetHistory || hasPortfolioBudgetHistory || hasProjectAnnualBudgetHistory
            ? "This Tenant has persisted fiscal-year budget records. Changing the fiscal year start month is blocked to protect historical periods. Contact the database owner for an approved fiscal-calendar migration."
            : null;
    }
}
