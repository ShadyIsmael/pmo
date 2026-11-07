using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.DataProtection;
using NEXUS.Api.Security;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class DataProtectionKeyRingRegistrationTests
{
    [Fact]
    public void Production_requires_explicit_stable_application_name()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var error = Assert.Throws<InvalidOperationException>(() =>
            services.AddConfiguredDataProtection(configuration, new TestHostEnvironment()));

        Assert.Contains("DataProtection:ApplicationName", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_requires_explicit_shared_keyring_path()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:ApplicationName"] = "nexus-production"
            })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(() =>
            services.AddConfiguredDataProtection(configuration, new TestHostEnvironment()));

        Assert.Contains("DataProtection:KeyRingPath", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_rejects_relative_keyring_path_before_certificate_lookup()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:ApplicationName"] = "nexus-production",
                ["DataProtection:KeyRingPath"] = ".\\keys"
            })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(() =>
            services.AddConfiguredDataProtection(configuration, new TestHostEnvironment()));

        Assert.Contains("must be an absolute path", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_requires_key_encryption_certificate_for_a_configured_keyring()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:ApplicationName"] = "nexus-production",
                ["DataProtection:KeyRingPath"] = Path.GetTempPath()
            })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(() =>
            services.AddConfiguredDataProtection(configuration, new TestHostEnvironment()));

        Assert.Contains("DataProtection:KeyEncryptionCertificateThumbprint", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Independent_instances_share_protected_values_through_the_configured_keyring_and_application_name()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"nexus-dp-{Guid.NewGuid():N}");
        var keyRingPath = Path.Combine(testRoot, "shared-keyring");
        Directory.CreateDirectory(testRoot);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:ApplicationName"] = "nexus-shared-test-app",
                ["DataProtection:KeyRingPath"] = keyRingPath
            })
            .Build();

        try
        {
            using (var firstInstance = CreateProvider(configuration, Path.Combine(testRoot, "instance-a")))
            {
                var firstProtector = firstInstance.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("NEXUS.Modules.Integrations.Secrets.v1");
                var ciphertext = firstProtector.Protect("integration-secret");

                using var secondInstance = CreateProvider(configuration, Path.Combine(testRoot, "instance-b"));
                var secondProtector = secondInstance.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("NEXUS.Modules.Integrations.Secrets.v1");

                Assert.DoesNotContain("integration-secret", ciphertext, StringComparison.Ordinal);
                Assert.Equal("integration-secret", secondProtector.Unprotect(ciphertext));
            }
        }
        finally
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Startup_check_fails_closed_when_the_configured_keyring_is_unavailable()
    {
        var missingKeyRingPath = Path.Combine(Path.GetTempPath(), $"nexus-dp-missing-{Guid.NewGuid():N}", "keys");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeyRingPath"] = missingKeyRingPath
            })
            .Build();
        var startupCheck = new DataProtectionKeyRingStartupCheck(
            configuration,
            DataProtectionProvider.Create("nexus-startup-check-tests"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => startupCheck.StartAsync(CancellationToken.None));

        Assert.Contains("directory is unavailable", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_check_accepts_a_writable_keyring_that_can_roundtrip_protection()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"nexus-dp-startup-{Guid.NewGuid():N}");
        var keyRingPath = Path.Combine(testRoot, "shared-keyring");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:ApplicationName"] = "nexus-startup-test-app",
                ["DataProtection:KeyRingPath"] = keyRingPath
            })
            .Build();

        try
        {
            using (var instance = CreateProvider(configuration, Path.Combine(testRoot, "instance")))
            {
                var startupCheck = new DataProtectionKeyRingStartupCheck(
                    configuration,
                    instance.GetRequiredService<IDataProtectionProvider>());

                await startupCheck.StartAsync(CancellationToken.None);
            }
        }
        finally
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
        }
    }

    private static ServiceProvider CreateProvider(IConfiguration configuration, string contentRoot)
    {
        Directory.CreateDirectory(contentRoot);
        return new ServiceCollection()
            .AddConfiguredDataProtection(
                configuration,
                new TestHostEnvironment
                {
                    EnvironmentName = "Testing",
                    ContentRootPath = contentRoot
                })
            .BuildServiceProvider();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "NEXUS.Api.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
