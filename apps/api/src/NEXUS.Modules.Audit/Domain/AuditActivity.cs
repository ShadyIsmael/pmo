using NEXUS.SharedKernel;

namespace NEXUS.Modules.Audit.Domain;

public class AuditActivity : BaseEntity
{
    public Guid AuditEntryId { get; set; }
    public AuditEntry AuditEntry { get; set; } = null!;
    public string Activity { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
