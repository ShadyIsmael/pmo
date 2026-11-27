using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

/// <summary>
/// A hard spending cap within one Project Annual Budget. Lines are configured
/// while the parent budget is Draft and become immutable when it is approved.
/// </summary>
public sealed class ProjectAnnualBudgetLine : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Guid ProjectAnnualBudgetId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal AuthorizedLimit { get; set; }
}

