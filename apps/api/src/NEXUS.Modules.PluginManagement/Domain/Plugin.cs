using NEXUS.SharedKernel;

namespace NEXUS.Modules.PluginManagement.Domain;

public class Plugin : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Version { get; set; }
    public string? Author { get; set; }
    public string? AssemblyName { get; set; }
    public string? EntryPoint { get; set; }
    public string? Category { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<TenantPlugin> TenantPlugins { get; set; } = new List<TenantPlugin>();
    public ICollection<PluginDependency> Dependencies { get; set; } = new List<PluginDependency>();
}
