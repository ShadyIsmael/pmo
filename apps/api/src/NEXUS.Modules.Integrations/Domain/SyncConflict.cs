using NEXUS.SharedKernel;

namespace NEXUS.Modules.Integrations.Domain;

public class SyncConflict : BaseEntity
{
    public Guid IntegrationSyncId { get; set; }
    public IntegrationSync IntegrationSync { get; set; } = null!;
    public string EntityType { get; set; } = string.Empty;
    public string LocalValue { get; set; } = string.Empty;
    public string RemoteValue { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public string Status { get; set; } = string.Empty;
}
