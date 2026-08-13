using NEXUS.SharedKernel;

namespace NEXUS.Modules.RiskManagement.Domain;

public class RiskMitigation : BaseEntity
{
    public Guid RiskId { get; set; }
    public Risk Risk { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? Cost { get; set; }
}
