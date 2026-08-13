using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Domain;

public class CostApproval : BaseEntity
{
    public Guid? BudgetId { get; set; }
    public Budget? Budget { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string? Justification { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? RequestedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Comments { get; set; }
}
