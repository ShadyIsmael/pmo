using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.Validators;
using NEXUS.SharedKernel;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;

namespace NEXUS.Modules.ProjectManagement.ModuleRegistration;

public static class ProjectManagementModuleRegistration
{
    public static IServiceCollection AddProjectManagementModule(this IServiceCollection services)
    {
        services.AddScoped<ProjectService>();
        services.AddScoped<ProjectManagerMembershipSynchronizer>();
        services.AddScoped<ProjectSponsorMembershipSynchronizer>();
        services.AddScoped<IProjectService>(sp => sp.GetRequiredService<ProjectService>());
        services.AddScoped<IProjectReferenceLookup>(sp => sp.GetRequiredService<ProjectService>());
        services.AddScoped<IProjectPublicIdLookup>(sp => sp.GetRequiredService<ProjectService>());
        services.AddScoped<IMilestoneService, MilestoneService>();
        services.AddScoped<IBaselineService, BaselineService>();
        services.AddScoped<IProjectSnapshotService, ProjectSnapshotService>();
        services.AddScoped<IProjectHandoverRequestService, ProjectHandoverRequestService>();
        services.AddScoped<IProjectChangeRequestService, ProjectChangeRequestWorkflowService>();
        services.AddScoped<IProjectChangeRequestWorkflowProvisioningService, ProjectChangeRequestWorkflowProvisioningService>();
        services.AddScoped<IWorkflowEntityAdapter, ProjectChangeRequestWorkflowAdapter>();
        services.AddScoped<IWorkflowEntityMetadataProvider, ProjectChangeRequestWorkflowMetadataProvider>();
        services.AddScoped<IWorkflowDefinitionConsumerValidator, ProjectChangeRequestWorkflowConsumerValidator>();
        services.AddScoped<IProjectBaselineService, ProjectBaselineService>();
        services.AddScoped<IProjectStatusReportService, ProjectStatusReportService>();
        services.AddScoped<IProjectStatusReportWorkflowProvisioningService, ProjectStatusReportWorkflowProvisioningService>();
        services.AddScoped<IWorkflowEntityAdapter, ProjectStatusReportWorkflowAdapter>();
        services.AddScoped<IWorkflowEntityMetadataProvider, ProjectStatusReportWorkflowMetadataProvider>();
        services.AddScoped<IWorkflowDefinitionConsumerValidator, ProjectStatusReportWorkflowConsumerValidator>();
        services.AddScoped<IProjectReportingService, ProjectReportingService>();
        services.AddScoped<IProjectOverviewService, ProjectOverviewService>();
        services.AddScoped<IStakeholderProjectReadService, StakeholderProjectReadService>();
        services.AddScoped<IStakeholderInvitationService, StakeholderInvitationService>();
        services.AddScoped<IProjectStakeholderService, ProjectStakeholderService>();
        services.AddScoped<IProjectPeopleService, ProjectPeopleService>();
        services.AddScoped<TimeEntryService>();
        services.AddScoped<ITimeEntryService>(sp => sp.GetRequiredService<TimeEntryService>());
        services.AddScoped<ITimerService, TimerService>();
        services.AddScoped<ActualWorkCostLookup>();
        services.AddScoped<IActualWorkCostLookup>(sp => sp.GetRequiredService<ActualWorkCostLookup>());
        services.AddScoped<IPlannedWorkCostLookup>(sp => sp.GetRequiredService<ActualWorkCostLookup>());
        
        services.AddScoped<IProjectResourcePlanningContextLookup, ProjectResourcePlanningContextLookup>();
        services.AddScoped<CreateProjectValidator>();
        services.AddScoped<UpdateProjectValidator>();
        services.AddScoped<CreateProjectMemberValidator>();
        services.AddScoped<UpdateProjectMemberValidator>();
        services.AddScoped<CreateProjectHandoverRequestValidator>();
        services.AddScoped<RejectProjectHandoverRequestValidator>();
        services.AddScoped<CreateProjectChangeRequestValidator>();
        services.AddScoped<ApproveProjectChangeRequestValidator>();
        services.AddScoped<RejectProjectChangeRequestValidator>();
        
        return services;
    }
}
