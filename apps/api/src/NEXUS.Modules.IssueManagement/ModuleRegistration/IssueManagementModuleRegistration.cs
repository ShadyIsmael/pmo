using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.IssueManagement.Application;

namespace NEXUS.Modules.IssueManagement.ModuleRegistration;

public static class IssueManagementModuleRegistration
{
    public static IServiceCollection AddIssueManagementModule(this IServiceCollection services)
    {
        services.AddScoped<IIssueService, IssueService>();
        return services;
    }
}
