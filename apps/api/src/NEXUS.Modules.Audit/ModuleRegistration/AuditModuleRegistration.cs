using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Audit.Application;

namespace NEXUS.Modules.Audit.ModuleRegistration;

public static class AuditModuleRegistration
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services)
    {
        services.AddScoped<IAuditService, AuditService>();
        return services;
    }
}
