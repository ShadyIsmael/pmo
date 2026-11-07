using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.ProjectPlanning.Application.Import;

namespace NEXUS.Modules.ProjectPlanning.MppAdapter.ModuleRegistration;

public static class ProjectPlanningMppAdapterRegistration
{
    public static IServiceCollection AddProjectPlanningMppAdapter(this IServiceCollection services)
    {
        services.AddScoped<IPlanningImportProvider, MppPlanningImportProvider>();
        return services;
    }
}
