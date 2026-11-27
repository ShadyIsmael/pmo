using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Organization.Application;
using NEXUS.Modules.ProjectManagement.Application;

namespace NEXUS.Persistence;

public static class PhaseGatePersistenceRegistration
{
    public static IServiceCollection AddPhaseGatePersistence(this IServiceCollection services)
    {
        services.AddScoped<IProjectPhaseGatePhasePairCatalog, EfProjectPhaseGatePhasePairCatalog>();
        services.AddScoped<IApprovedTemplateCatalog, EfApprovedTemplateCatalog>();
        services.AddScoped<IProjectPhaseGateConfigurationReader, EfProjectPhaseGateConfigurationReader>();
        services.AddScoped<IDocumentCategoryReferenceValidator, EfDocumentCategoryReferenceValidator>();
        return services;
    }
}
