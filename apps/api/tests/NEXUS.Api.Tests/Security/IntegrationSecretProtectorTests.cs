using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class IntegrationSecretProtectorTests
{
    [Fact]
    public void Protect_and_unprotect_round_trip_and_marks_ciphertext()
    {
        var provider = DataProtectionProvider.Create("nexus-tests");
        var protector = new NEXUS.Modules.Integrations.Infrastructure.IntegrationSecretProtector(provider);

        var protectedValue = protector.Protect("top-secret-api-key");

        Assert.NotNull(protectedValue);
        Assert.StartsWith("NEXUSINTDP1:", protectedValue, StringComparison.Ordinal);
        Assert.DoesNotContain("top-secret-api-key", protectedValue, StringComparison.Ordinal);
        Assert.True(protector.IsProtected(protectedValue));
        Assert.True(protector.TryUnprotect(protectedValue, out var plaintext));
        Assert.Equal("top-secret-api-key", plaintext);
    }

    [Fact]
    public void Legacy_plaintext_fails_closed_until_the_reviewed_data_migration_runs()
    {
        var provider = DataProtectionProvider.Create("nexus-tests");
        var protector = new NEXUS.Modules.Integrations.Infrastructure.IntegrationSecretProtector(provider);

        Assert.False(protector.IsProtected("legacy-plaintext"));
        Assert.False(protector.TryUnprotect("legacy-plaintext", out var plaintext));
        Assert.Null(plaintext);
    }

    [Fact]
    public void Protect_rejects_ciphertext_with_a_marker_but_invalid_payload()
    {
        var provider = DataProtectionProvider.Create("nexus-tests");
        var protector = new NEXUS.Modules.Integrations.Infrastructure.IntegrationSecretProtector(provider);

        Assert.Throws<System.Security.Cryptography.CryptographicException>(
            () => protector.Protect("NEXUSINTDP1:corrupt-payload"));
    }

    [Fact]
    public void Unreadable_ciphertext_fails_closed_without_throwing()
    {
        var providerA = DataProtectionProvider.Create("nexus-tests-a");
        var providerB = DataProtectionProvider.Create("nexus-tests-b");
        var writer = new NEXUS.Modules.Integrations.Infrastructure.IntegrationSecretProtector(providerA);
        var reader = new NEXUS.Modules.Integrations.Infrastructure.IntegrationSecretProtector(providerB);

        var protectedValue = writer.Protect("secret");

        Assert.False(reader.TryUnprotect(protectedValue, out var plaintext));
        Assert.Null(plaintext);
    }
}
