using NEXUS.SharedKernel;

namespace NEXUS.Modules.Integrations.Domain;

public class IntegrationWebhook : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Secret { get; set; }
    public string? Events { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastTriggeredAt { get; set; }
}
