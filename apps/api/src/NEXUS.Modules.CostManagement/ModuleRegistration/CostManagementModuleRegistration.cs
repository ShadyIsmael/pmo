using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.SharedKernel;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;

namespace NEXUS.Modules.CostManagement.ModuleRegistration;

public static class CostManagementModuleRegistration
{
    public static IServiceCollection AddCostManagementModule(this IServiceCollection services)
    {
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<ICostService, CostService>();
        services.AddScoped<ICommitmentService, CommitmentService>();
        services.AddScoped<IProjectFinancialLookup, ProjectFinancialLookup>();
        services.AddScoped<IProjectEvmDirectCostLookup, ProjectEvmDirectCostLookup>();
        services.AddScoped<IGlobalFinancialSummaryService, GlobalFinancialSummaryService>();
        services.AddScoped<IBudgetRevisionService, BudgetRevisionService>();
        services.AddScoped<IWorkflowEntityAdapter, FinanceWorkflowAdapter>();
        services.AddScoped<IWorkflowEntityMetadataProvider, FinanceWorkflowMetadataProvider>();
        services.AddScoped<IWorkflowDefinitionConsumerValidator, FinanceWorkflowConsumerValidator>();
        return services;
    }
}
