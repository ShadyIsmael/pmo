using NEXUS.SharedKernel;

namespace NEXUS.Modules.PluginManagement.Domain;

public class TenantPlugin : BaseEntity
{
    public Guid PluginId { get; set; }
    public Plugin Plugin { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    public string? Config { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }
}
