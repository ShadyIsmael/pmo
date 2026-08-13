using NEXUS.SharedKernel;

namespace NEXUS.Modules.Audit.Domain;

public class AuditEntry : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Changes { get; set; }
    public string? OriginalValues { get; set; }
    public string? NewValues { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ICollection<AuditActivity> Activities { get; set; } = new List<AuditActivity>();
}
