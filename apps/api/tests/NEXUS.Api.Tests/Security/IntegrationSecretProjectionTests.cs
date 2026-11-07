using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using NEXUS.Modules.Integrations.Application;
using NEXUS.Modules.Integrations.Infrastructure;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public class IntegrationSecretProjectionTests
{
    [Fact]
    public void Dto_serialization_never_contains_secret_properties_even_with_protected_values()
    {
        var provider = DataProtectionProvider.Create("nexus-tests");
        var protector = new IntegrationSecretProtector(provider);
        var protectedKey = protector.Protect("secret-key");
        var protectedSecret = protector.Protect("secret-secret");

        Assert.StartsWith("NEXUSINTDP1:", protectedKey, StringComparison.Ordinal);
        Assert.True(protector.TryUnprotect(protectedKey, out var keyPlaintext));
        Assert.Equal("secret-key", keyPlaintext);

        var dto = new IntegrationConnectionDto
        {
            Name = "GitLab",
            Provider = "gitlab",
            HasApiKey = !string.IsNullOrEmpty(protectedKey),
            HasApiSecret = !string.IsNullOrEmpty(protectedSecret)
        };

        var json = JsonSerializer.Serialize(dto);
        Assert.DoesNotContain("secret-key", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ApiKey\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ApiSecret\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("NEXUSINTDP1:", json, StringComparison.Ordinal);
    }
}
