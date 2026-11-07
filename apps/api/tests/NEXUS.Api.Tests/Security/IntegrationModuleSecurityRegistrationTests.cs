using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Integrations.Infrastructure;
using NEXUS.Modules.Integrations.ModuleRegistration;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class IntegrationModuleSecurityRegistrationTests
{
    [Fact]
    public void Integrations_module_requires_host_configured_data_protection()
    {
        var services = new ServiceCollection();

        services.AddIntegrationsModule();

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IIntegrationSecretProtector));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IDataProtectionProvider));
    }
}
