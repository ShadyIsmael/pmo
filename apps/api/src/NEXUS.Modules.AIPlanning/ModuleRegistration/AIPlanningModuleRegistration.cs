using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.AIPlanning.Application;
using NEXUS.Modules.AIPlanning.Infrastructure;

namespace NEXUS.Modules.AIPlanning.ModuleRegistration;

public static class AIPlanningModuleRegistration
{
    public static IServiceCollection AddAIPlanningModule(this IServiceCollection services)
    {
        services.AddScoped<IAIPlanningService, AIPlanningService>();
        services.AddScoped<IAIProvider, MockAIProvider>();
        return services;
    }
}
