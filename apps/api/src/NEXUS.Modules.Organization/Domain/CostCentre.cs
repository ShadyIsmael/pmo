using NEXUS.SharedKernel;

namespace NEXUS.Modules.Organization.Domain;

public class CostCentre : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? BusinessUnitId { get; set; }
    public BusinessUnit? BusinessUnit { get; set; }
    public decimal? AnnualBudget { get; set; }
    public bool IsActive { get; set; } = true;
}
