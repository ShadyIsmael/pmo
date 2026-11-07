using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NEXUS.Api.Security;

public static class DataProtectionKeyRingRegistration
{
    public static IServiceCollection AddConfiguredDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var allowLocalKeyRing = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        var applicationName = configuration["DataProtection:ApplicationName"];
        if (string.IsNullOrWhiteSpace(applicationName))
        {
            if (!allowLocalKeyRing)
            {
                throw new InvalidOperationException(
                    "DataProtection:ApplicationName must be configured with the same stable value on every application instance.");
            }

            // This matches ASP.NET Core's default application discriminator and preserves
            // existing Development protection purposes unless the operator opts in to an
            // explicit local value.
            applicationName = environment.ContentRootPath;
        }

        var builder = services.AddDataProtection().SetApplicationName(applicationName);
        var keyRingPath = configuration["DataProtection:KeyRingPath"];
        if (string.IsNullOrWhiteSpace(keyRingPath))
        {
            if (allowLocalKeyRing) return services;
            throw new InvalidOperationException(
                "DataProtection:KeyRingPath must point to a durable key repository shared by every application instance.");
        }

        if (!Path.IsPathFullyQualified(keyRingPath))
        {
            throw new InvalidOperationException("DataProtection:KeyRingPath must be an absolute path.");
        }

        var keyRingDirectory = new DirectoryInfo(Path.GetFullPath(keyRingPath));
        if (allowLocalKeyRing)
        {
            Directory.CreateDirectory(keyRingDirectory.FullName);
        }
        else if (!keyRingDirectory.Exists)
        {
            throw new InvalidOperationException(
                "The configured Data Protection key-ring directory must be provisioned with restricted permissions before startup.");
        }

        builder.PersistKeysToFileSystem(keyRingDirectory);

        if (allowLocalKeyRing) return services;

        var activeThumbprint = configuration["DataProtection:KeyEncryptionCertificateThumbprint"];
        if (string.IsNullOrWhiteSpace(activeThumbprint))
        {
            throw new InvalidOperationException(
                "DataProtection:KeyEncryptionCertificateThumbprint must identify the certificate used to protect the shared key ring.");
        }

        var activeCertificate = FindCertificateWithPrivateKey(activeThumbprint);
        var certificates = new List<X509Certificate2> { activeCertificate };
        var priorThumbprints = configuration
            .GetSection("DataProtection:KeyDecryptionCertificateThumbprints")
            .Get<string[]>() ?? Array.Empty<string>();
        foreach (var thumbprint in priorThumbprints.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var certificate = FindCertificateWithPrivateKey(thumbprint);
            if (!certificates.Any(existing => string.Equals(
                    NormalizeThumbprint(existing.Thumbprint),
                    NormalizeThumbprint(certificate.Thumbprint),
                    StringComparison.OrdinalIgnoreCase)))
            {
                certificates.Add(certificate);
            }
        }

        builder
            .ProtectKeysWithCertificate(activeCertificate)
            .UnprotectKeysWithAnyCertificate(certificates.ToArray());
        services.Insert(0, ServiceDescriptor.Singleton<IHostedService, DataProtectionKeyRingStartupCheck>());

        return services;
    }

    private static X509Certificate2 FindCertificateWithPrivateKey(string thumbprint)
    {
        var normalizedThumbprint = NormalizeThumbprint(thumbprint);
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        var certificate = store.Certificates
            .Cast<X509Certificate2>()
            .FirstOrDefault(candidate =>
                candidate.HasPrivateKey
                && string.Equals(
                    NormalizeThumbprint(candidate.Thumbprint),
                    normalizedThumbprint,
                    StringComparison.OrdinalIgnoreCase));

        if (certificate is null)
        {
            throw new InvalidOperationException(
                "The configured Data Protection certificate was not found in the application user's certificate store with its private key.");
        }

        return new X509Certificate2(certificate);
    }

    private static string NormalizeThumbprint(string? value) =>
        new((value ?? string.Empty).Where(Uri.IsHexDigit).ToArray());
}