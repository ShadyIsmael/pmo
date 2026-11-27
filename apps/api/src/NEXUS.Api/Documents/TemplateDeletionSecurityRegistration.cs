using Microsoft.Extensions.DependencyInjection;

namespace NEXUS.Api.Documents;

public static class TemplateDeletionSecurityRegistration
{
    public static IServiceCollection AddTemplateDeletionSecurityAdapters(this IServiceCollection services)
    {
        services.AddScoped<ITemplateDeletionApproverResolver, TemplateDeletionApproverResolver>();
        services.AddScoped<ITemplateDeletionNotificationPublisher, TemplateDeletionNotificationPublisher>();
        return services;
    }
}
