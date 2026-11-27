using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.Infrastructure;

namespace NEXUS.Modules.Documents.ModuleRegistration;

public static class DocumentsModuleRegistration
{
    public static IServiceCollection AddDocumentsModule(this IServiceCollection services)
    {
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddKeyedSingleton<IStorageProvider>(LocalStorageProvider.Identifier, (sp, _) =>
        {
            var configuration = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            var root = configuration["Storage:DocumentsRoot"]
                ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "documents");
            return new LocalStorageProvider(root);
        });
        services.AddSingleton<IStorageProvider>(sp =>
        {
            var configuration = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            var configuredProviderId = configuration["Storage:Provider"]?.Trim();
            var providerId = string.IsNullOrEmpty(configuredProviderId)
                || string.Equals(configuredProviderId, LocalStorageProvider.Identifier, StringComparison.OrdinalIgnoreCase)
                ? LocalStorageProvider.Identifier
                : configuredProviderId;

            var provider = sp.GetKeyedService<IStorageProvider>(providerId)
                ?? throw new InvalidOperationException($"The configured document storage provider '{providerId}' is not registered.");
            if (!string.Equals(provider.ProviderId, providerId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The registered document storage provider identifier does not match its DI key.");

            return provider;
        });
        return services;
    }
}
