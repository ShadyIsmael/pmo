using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Documents.Application;

namespace NEXUS.Api.Documents;

public static class TemplateDeletionWorkflowRegistration
{
    public static IServiceCollection AddTemplateDeletionWorkflow(this IServiceCollection services)
    {
        services.AddTemplateDeletionSecurityAdapters();
        services.AddScoped<ITemplateDeletionReferenceValidator, EfTemplateDeletionReferenceValidator>();
        services.AddScoped<ITemplateDeletionWorkflowService, TemplateDeletionWorkflowService>();
        services.AddScoped<ITemplateDeletionStorageCleanupProcessor, TemplateDeletionStorageCleanupProcessor>();
        services.AddHostedService<TemplateDeletionStorageCleanupWorker>();
        return services;
    }
}
