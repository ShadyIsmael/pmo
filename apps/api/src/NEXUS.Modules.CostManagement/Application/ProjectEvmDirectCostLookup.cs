using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Application;

/// <summary>
/// EVM actual direct costs are non-labor CostEntry records in Actual state
/// that have been Posted or Verified by the financial workflow. Labor-linked
/// entries are excluded because TimeEntry × ResourceCostProfile is the
/// authoritative labor source. Tax is excluded, matching the Finance UI.
/// </summary>
public sealed class ProjectEvmDirectCostLookup : IProjectEvmDirectCostLookup
{
    private readonly DbContext _context;

    public ProjectEvmDirectCostLookup(DbContext context) => _context = context;

    public async Task<ProjectEvmDirectCostSummary> GetAsync(
        Guid tenantId,
        Guid projectId,
        string projectCurrency,
        DateTime statusDate,
        CancellationToken cancellationToken = default)
    {
        var entries = await _context.Set<CostEntry>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted
                && item.IncurredDate <= statusDate.Date)
            .ToListAsync(cancellationToken);

        var actualEntries = entries.Where(item =>
                string.Equals(item.Type, "Actual", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(item.Status, "Posted", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Status, "Verified", StringComparison.OrdinalIgnoreCase))
                && !item.ResourceId.HasValue
                && !IsLaborCategory(item.Category)
                && !IsLaborCategory(item.SubCategory))
            .ToList();
        var currency = NormalizeCurrency(projectCurrency);
        var matching = actualEntries.Where(item => NormalizeCurrency(item.Currency) == currency).ToList();

        return new ProjectEvmDirectCostSummary(
            matching.Sum(item => item.Amount),
            actualEntries.Count > 0,
            actualEntries.Any(item => NormalizeCurrency(item.Currency) != currency));
    }

    private static bool IsLaborCategory(string? category)
        => category is not null && new[] { "labor", "labour", "payroll", "salary", "wage" }
            .Any(term => category.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeCurrency(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
}
