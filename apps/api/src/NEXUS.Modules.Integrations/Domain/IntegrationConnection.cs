using NEXUS.SharedKernel;

namespace NEXUS.Modules.Integrations.Domain;

public class IntegrationConnection : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string? AuthType { get; set; }
    public string? Config { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncAt { get; set; }
    public string? Status { get; set; }
    public ICollection<IntegrationSync> Syncs { get; set; } = new List<IntegrationSync>();
}
