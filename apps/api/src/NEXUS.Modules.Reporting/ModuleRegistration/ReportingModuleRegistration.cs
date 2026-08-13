using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Reporting.Application;

namespace NEXUS.Modules.Reporting.ModuleRegistration;

public static class ReportingModuleRegistration
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services)
    {
        services.AddScoped<IReportService, ReportService>();
        return services;
    }
}
