using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.ProjectPlanning.Application;

namespace NEXUS.Modules.ProjectPlanning.ModuleRegistration;

public static class ProjectPlanningModuleRegistration
{
    public static IServiceCollection AddProjectPlanningModule(this IServiceCollection services)
    {
        services.AddScoped<IPlanService, PlanService>();
        services.AddScoped<IPlanWbsService, PlanWbsService>();
        services.AddScoped<IPlanActivityService, PlanActivityService>();
        services.AddScoped<IPlanDependencyService, PlanDependencyService>();
        return services;
    }
}