using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Portfolio.Application;

namespace NEXUS.Modules.Portfolio.ModuleRegistration;

public static class PortfolioModuleRegistration
{
    public static IServiceCollection AddPortfolioModule(this IServiceCollection services)
    {
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<IProgramService, ProgramService>();
        services.AddScoped<IManagerHandoverService, ManagerHandoverService>();
        return services;
    }
}
