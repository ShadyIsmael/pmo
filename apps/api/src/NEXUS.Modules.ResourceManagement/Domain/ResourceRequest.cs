using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Domain;

public class ResourceRequest : BaseEntity
{
    public Guid? ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? SkillRequirements { get; set; }
    public decimal RequiredAllocation { get; set; }
    public DateTime RequiredFrom { get; set; }
    public DateTime RequiredTo { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Justification { get; set; }
    public Guid? RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}
