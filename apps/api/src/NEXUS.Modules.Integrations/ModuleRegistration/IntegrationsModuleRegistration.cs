using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Integrations.Application;
using NEXUS.Modules.Integrations.Infrastructure;

namespace NEXUS.Modules.Integrations.ModuleRegistration;

public static class IntegrationsModuleRegistration
{
    public static IServiceCollection AddIntegrationsModule(this IServiceCollection services)
    {
        services.AddScoped<IIntegrationService, IntegrationService>();
        services.AddHttpClient(DirectoryProviderHttpClients.Entra);
        services.AddHttpClient(DirectoryProviderHttpClients.Ldap);
        services.AddOptions<DirectoryLdapOptions>().BindConfiguration(DirectoryLdapOptions.SectionName);
        services.AddScoped<IDirectorySecretResolver, ConfigurationDirectorySecretResolver>();
        services.AddScoped<IDirectoryProvider, EntraDirectoryProvider>();
        services.AddScoped<IDirectoryProvider, LdapDirectoryProvider>();
        services.AddScoped<IDirectoryIntegrationService, DirectoryIntegrationService>();
        return services;
    }
}
