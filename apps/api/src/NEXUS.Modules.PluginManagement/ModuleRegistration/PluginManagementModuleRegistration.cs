using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.PluginManagement.Application;

namespace NEXUS.Modules.PluginManagement.ModuleRegistration;

public static class PluginManagementModuleRegistration
{
    public static IServiceCollection AddPluginManagementModule(this IServiceCollection services)
    {
        services.AddScoped<IPluginRegistry, PluginRegistryService>();
        return services;
    }
}
