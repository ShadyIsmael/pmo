using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Domain;

public class Escalation : BaseEntity
{
    public Guid? SLAId { get; set; }
    public SLA? SLA { get; set; }
    public string? Name { get; set; }
    public int EscalationLevel { get; set; }
    public int? EscalateAfterHours { get; set; }
    public Guid? NotifyUserId { get; set; }
    public string? NotifyRole { get; set; }
    public string? NotificationType { get; set; }
}
