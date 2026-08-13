using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.CostManagement.Application;

namespace NEXUS.Modules.CostManagement.ModuleRegistration;

public static class CostManagementModuleRegistration
{
    public static IServiceCollection AddCostManagementModule(this IServiceCollection services)
    {
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<ICostService, CostService>();
        return services;
    }
}
