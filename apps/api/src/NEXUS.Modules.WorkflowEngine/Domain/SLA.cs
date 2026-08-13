using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Domain;

public class SLA : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? Priority { get; set; }
    public int TargetHours { get; set; }
    public int? WarningHours { get; set; }
    public int? EscalationHours { get; set; }
    public string? BusinessHours { get; set; }
    public bool IsActive { get; set; } = true;
}
