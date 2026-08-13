using NEXUS.SharedKernel;

namespace NEXUS.Modules.PluginManagement.Domain;

public class PluginDependency : BaseEntity
{
    public Guid PluginId { get; set; }
    public Plugin Plugin { get; set; } = null!;
    public string DependencyName { get; set; } = string.Empty;
    public string? MinVersion { get; set; }
    public string? MaxVersion { get; set; }
}
