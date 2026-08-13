using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Approvals.Application;

namespace NEXUS.Modules.Approvals.ModuleRegistration;

public static class ApprovalsModuleRegistration
{
    public static IServiceCollection AddApprovalsModule(this IServiceCollection services)
    {
        services.AddScoped<IApprovalService, ApprovalService>();
        return services;
    }
}
