using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Meetings.Application;

namespace NEXUS.Modules.Meetings.ModuleRegistration;

public static class MeetingsModuleRegistration
{
    public static IServiceCollection AddMeetingsModule(this IServiceCollection services)
    {
        services.AddScoped<IMeetingService, MeetingService>();
        return services;
    }
}
