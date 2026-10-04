using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.RiskManagement.Application;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;

namespace NEXUS.Modules.RiskManagement.ModuleRegistration;

public static class RiskManagementModuleRegistration
{
    public static IServiceCollection AddRiskManagementModule(this IServiceCollection services)
    {
        services.AddScoped<IRiskService, RiskService>();
        services.AddScoped<IWorkflowEntityAdapter, RiskWorkflowAdapter>();
        services.AddScoped<IWorkflowEntityMetadataProvider, RiskWorkflowMetadataProvider>();
        services.AddScoped<IWorkflowDefinitionConsumerValidator, RiskWorkflowConsumerValidator>();
        return services;
    }
}
