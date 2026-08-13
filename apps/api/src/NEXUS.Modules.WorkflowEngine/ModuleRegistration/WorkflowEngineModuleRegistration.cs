using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.WorkflowEngine.Application;

namespace NEXUS.Modules.WorkflowEngine.ModuleRegistration;

public static class WorkflowEngineModuleRegistration
{
    public static IServiceCollection AddWorkflowEngineModule(this IServiceCollection services)
    {
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IWorkflowDesignerService, WorkflowDesignerService>();
        return services;
    }
}
