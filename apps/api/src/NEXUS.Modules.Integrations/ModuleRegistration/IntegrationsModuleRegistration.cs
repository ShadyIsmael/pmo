using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Integrations.Application;

namespace NEXUS.Modules.Integrations.ModuleRegistration;

public static class IntegrationsModuleRegistration
{
    public static IServiceCollection AddIntegrationsModule(this IServiceCollection services)
    {
        services.AddScoped<IIntegrationService, IntegrationService>();
        return services;
    }
}
