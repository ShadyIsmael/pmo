using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Domain.Scheduling;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.ModuleRegistration;

public static class ProjectPlanningModuleRegistration
{
    public static IServiceCollection AddProjectPlanningModule(this IServiceCollection services)
    {
        services.AddScoped<IPlanService, PlanService>();
        services.AddScoped<IProjectPlanProgressLookup, ProjectPlanProgressLookup>();
        services.AddScoped<IProjectEvmPlanLookup, ProjectEvmPlanLookup>();
        services.AddScoped<IProjectPlanMilestoneLookup, ProjectPlanMilestoneLookup>();
        services.AddScoped<IPlanWbsService, PlanWbsService>();
        services.AddScoped<IPlanActivityService, PlanActivityService>();
        services.AddScoped<IPlanGridCommandHistoryService, PlanGridCommandHistoryService>();
        services.AddScoped<IPlanDependencyService, PlanDependencyService>();
        services.AddSingleton<AutoSchedulingEngine>();
        services.AddScoped<IPlanSchedulingService, PlanSchedulingService>();
        services.AddScoped<SummaryTaskRollupService>();
        services.AddSingleton<CriticalPathAnalyzer>();
        services.AddScoped<IPlanSchedulingAnalysisService, PlanSchedulingAnalysisService>();
        services.AddScoped<IPlanAssignmentService, PlanAssignmentService>();
        services.AddScoped<IPlanningCalendarService, PlanningCalendarService>();
        services.AddScoped<IPlanningTablePreferenceService, PlanningTablePreferenceService>();
        services.AddScoped<IPlanVersionService, PlanVersionService>();
        services.AddScoped<IPlanVersionComparisonService, PlanVersionComparisonService>();
        services.AddScoped<IPlanningImportService, PlanningImportService>();
        return services;
    }
}
