using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.ModuleRegistration;

public static class CostManagementModuleRegistration
{
    public static IServiceCollection AddCostManagementModule(this IServiceCollection services)
    {
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<ICostService, CostService>();
        services.AddScoped<ICommitmentService, CommitmentService>();
        services.AddScoped<IProjectFinancialLookup, ProjectFinancialLookup>();
        services.AddScoped<IGlobalFinancialSummaryService, GlobalFinancialSummaryService>();
        services.AddScoped<IBudgetRevisionService, BudgetRevisionService>();
        return services;
    }
}
