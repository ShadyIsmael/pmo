using NEXUS.SharedKernel;

namespace NEXUS.Modules.Integrations.Domain;

public class IntegrationSync : BaseEntity
{
    public Guid IntegrationConnectionId { get; set; }
    public IntegrationConnection IntegrationConnection { get; set; } = null!;
    public string Direction { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? RecordsProcessed { get; set; }
    public int? RecordsFailed { get; set; }
    public string? ErrorLog { get; set; }
}
