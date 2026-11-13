using Microsoft.Extensions.DependencyInjection;
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
        return services;
    }
}
