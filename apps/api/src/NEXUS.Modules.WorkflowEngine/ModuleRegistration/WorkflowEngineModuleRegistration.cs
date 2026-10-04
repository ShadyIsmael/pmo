using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.WorkflowEngine.Application;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Guards;
using NEXUS.Modules.WorkflowEngine.Application.Actions;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.Modules.WorkflowEngine.Infrastructure;

namespace NEXUS.Modules.WorkflowEngine.ModuleRegistration;

public static class WorkflowEngineModuleRegistration
{
    public static IServiceCollection AddWorkflowEngineModule(this IServiceCollection services)
    {
        services.AddScoped<WorkflowConfigurationRegistry>();
        services.AddScoped<IWorkflowGuardHandlerRegistry>(sp => sp.GetRequiredService<WorkflowConfigurationRegistry>());
        services.AddScoped<IWorkflowActionHandlerRegistry>(sp => sp.GetRequiredService<WorkflowConfigurationRegistry>());
        services.AddScoped<WorkflowDefinitionValidator>();

        services.AddScoped<IWorkflowEntityAdapterRegistry, WorkflowEntityAdapterRegistry>();

        services.AddScoped<IWorkflowExecutionService, WorkflowExecutionService>();
        services.AddScoped<IWorkflowInstanceService, WorkflowInstanceService>();
        services.AddScoped<IWorkflowTransitionQueryService, WorkflowTransitionQueryService>();
        services.AddScoped<IWorkflowManagementService, WorkflowManagementService>();
        services.AddScoped<IProjectWorkflowConfigurationService, ProjectWorkflowConfigurationService>();

        services.AddScoped<IWorkflowGuardHandler, RequirePermissionGuardHandler>();
        services.AddScoped<IWorkflowGuardHandler, RequireRoleGuardHandler>();
        services.AddScoped<IWorkflowGuardHandler, RequireCommentGuardHandler>();
        services.AddScoped<IWorkflowGuardHandler, RequireFactGuardHandler>();

        services.AddScoped<IWorkflowActionHandler, DomainProjectionActionHandler>();
        services.AddScoped<IWorkflowActionHandler, AuditProjectionActionHandler>();

        return services;
    }
}
