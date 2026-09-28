using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.WorkItems.Application;
using NEXUS.Modules.WorkItems.Infrastructure;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkItems.ModuleRegistration;

public static class WorkItemsModuleRegistration
{
    public static IServiceCollection AddWorkItemsModule(this IServiceCollection services)
    {
        services.AddScoped<IWorkItemService, WorkItemService>();
        services.AddScoped<IWorkItemWorkflowProvisioningService, WorkItemWorkflowProvisioningService>();
        services.AddScoped<IWorkflowEntityAdapter, WorkItemWorkflowAdapter>();
        services.AddScoped<IWorkflowEntityMetadataProvider, WorkItemWorkflowMetadataProvider>();
        services.AddScoped<IWorkflowDefinitionConsumerValidator, WorkItemWorkflowConsumerValidator>();
        services.AddScoped<IWorkItemReferenceLookup, WorkItemReferenceLookup>();
        return services;
    }
}
