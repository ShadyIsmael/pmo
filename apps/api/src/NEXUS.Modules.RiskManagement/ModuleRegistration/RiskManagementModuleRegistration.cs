using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.RiskManagement.Application;

namespace NEXUS.Modules.RiskManagement.ModuleRegistration;

public static class RiskManagementModuleRegistration
{
    public static IServiceCollection AddRiskManagementModule(this IServiceCollection services)
    {
        services.AddScoped<IRiskService, RiskService>();
        return services;
    }
}
