using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Integrations.Application;
using NEXUS.Modules.Integrations.Infrastructure;

namespace NEXUS.Modules.Integrations.ModuleRegistration;

public static class IntegrationsModuleRegistration
{
    public static IServiceCollection AddIntegrationsModule(this IServiceCollection services)
    {
        // The host composition root owns Data Protection key-ring configuration.
        // Registering the framework default here would silently allow an unconfigured
        // host to persist secrets with a local or ephemeral key ring.
        services.AddScoped<IIntegrationSecretProtector, IntegrationSecretProtector>();
        services.AddScoped<IntegrationSecretProtectionInterceptor>();
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
