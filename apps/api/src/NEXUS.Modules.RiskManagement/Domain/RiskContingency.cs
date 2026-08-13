using NEXUS.SharedKernel;

namespace NEXUS.Modules.RiskManagement.Domain;

public class RiskContingency : BaseEntity
{
    public Guid RiskId { get; set; }
    public Risk Risk { get; set; } = null!;
    public string Plan { get; set; } = string.Empty;
    public decimal? Budget { get; set; }
    public string? Trigger { get; set; }
    public Guid? OwnerId { get; set; }
    public string Status { get; set; } = string.Empty;
}
