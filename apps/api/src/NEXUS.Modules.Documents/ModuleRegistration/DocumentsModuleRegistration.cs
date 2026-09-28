using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.Infrastructure;

namespace NEXUS.Modules.Documents.ModuleRegistration;

public static class DocumentsModuleRegistration
{
    public static IServiceCollection AddDocumentsModule(this IServiceCollection services)
    {
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IStorageProvider>(sp =>
        {
            var configuration = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            var root = configuration["Storage:DocumentsRoot"] ?? Path.Combine(AppContext.BaseDirectory, "uploads", "documents");
            return new LocalStorageProvider(root);
        });
        return services;
    }
}
