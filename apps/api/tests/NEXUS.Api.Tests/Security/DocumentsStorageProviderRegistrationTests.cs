using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.ModuleRegistration;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class DocumentsStorageProviderRegistrationTests
{
    [Fact]
    public void Configured_external_provider_can_be_selected_without_changing_document_registration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Provider"] = "memory-test"
            })
            .Build();
        var externalProvider = new Mock<IStorageProvider>();
        externalProvider.Setup(provider => provider.ProviderId).Returns("memory-test");
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDocumentsModule();
        // Simulates a provider module registering after the Documents module.
        services.AddKeyedSingleton<IStorageProvider>("memory-test", externalProvider.Object);

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Same(externalProvider.Object, serviceProvider.GetRequiredService<IStorageProvider>());
    }
}
