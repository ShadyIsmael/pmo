using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Meetings.Application;

namespace NEXUS.Modules.Meetings.ModuleRegistration;

public static class MeetingsModuleRegistration
{
    public static IServiceCollection AddMeetingsModule(this IServiceCollection services)
    {
        services.AddOptions<MeetingSharingOptions>().BindConfiguration(MeetingSharingOptions.SectionName);
        services.AddScoped<IMeetingService, MeetingService>();
        services.AddScoped<IMeetingWorkspaceService, MeetingWorkspaceService>();
        services.AddScoped<IMeetingSharingService, MeetingSharingService>();
        return services;
    }
}
