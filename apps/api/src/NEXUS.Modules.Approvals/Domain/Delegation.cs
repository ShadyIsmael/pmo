using NEXUS.SharedKernel;

namespace NEXUS.Modules.Approvals.Domain;

public class Delegation : BaseEntity
{
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Reason { get; set; }
    public bool IsActive { get; set; } = true;
}
