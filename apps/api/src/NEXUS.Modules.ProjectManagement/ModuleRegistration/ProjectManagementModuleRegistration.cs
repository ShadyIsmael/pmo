using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.Validators;

namespace NEXUS.Modules.ProjectManagement.ModuleRegistration;

public static class ProjectManagementModuleRegistration
{
    public static IServiceCollection AddProjectManagementModule(this IServiceCollection services)
    {
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IMilestoneService, MilestoneService>();
        services.AddScoped<IBaselineService, BaselineService>();
        services.AddScoped<IProjectSnapshotService, ProjectSnapshotService>();
        services.AddScoped<IProjectHandoverRequestService, ProjectHandoverRequestService>();
        services.AddScoped<IProjectChangeRequestService, ProjectChangeRequestService>();
        services.AddScoped<IProjectBaselineService, ProjectBaselineService>();
        
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
