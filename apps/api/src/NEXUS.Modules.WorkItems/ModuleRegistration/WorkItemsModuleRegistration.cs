using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.WorkItems.Application;

namespace NEXUS.Modules.WorkItems.ModuleRegistration;

public static class WorkItemsModuleRegistration
{
    public static IServiceCollection AddWorkItemsModule(this IServiceCollection services)
    {
        services.AddScoped<IWorkItemService, WorkItemService>();
        return services;
    }
}
