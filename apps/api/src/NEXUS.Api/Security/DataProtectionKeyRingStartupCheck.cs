using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace NEXUS.Api.Security;

/// <summary>
/// Fails application startup when the configured shared key ring cannot persist or use
/// Data Protection keys. It does not prove that the path is shared across instances.
/// </summary>
public sealed class DataProtectionKeyRingStartupCheck(
    IConfiguration configuration,
    IDataProtectionProvider dataProtectionProvider) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keyRingPath = configuration["DataProtection:KeyRingPath"]
            ?? throw new InvalidOperationException("DataProtection:KeyRingPath is required.");
        var keyRingDirectory = Path.GetFullPath(keyRingPath);
        if (!Directory.Exists(keyRingDirectory))
        {
            throw new InvalidOperationException("The configured Data Protection key-ring directory is unavailable.");
        }

        VerifyDirectoryIsWritable(keyRingDirectory);
        VerifyDataProtectionRoundTrip();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void VerifyDirectoryIsWritable(string directory)
    {
        var probePath = Path.Combine(directory, $".nexus-dp-readiness-{Guid.NewGuid():N}.tmp");
        try
        {
            using var probe = new FileStream(
                probePath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);
            probe.WriteByte(0);
            probe.Flush(flushToDisk: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new InvalidOperationException(
                "The configured Data Protection key-ring directory is not writable by the application identity.",
                ex);
        }
        finally
        {
            try
            {
                if (File.Exists(probePath)) File.Delete(probePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The startup check reports inability to use the key ring above. A stale
                // empty probe file is not secret and has a unique, bounded name.
            }
        }
    }

    private void VerifyDataProtectionRoundTrip()
    {
        var probeText = Guid.NewGuid().ToString("N");
        var protector = dataProtectionProvider.CreateProtector("NEXUS.PMO.DataProtection.KeyRingReadiness.v1");
        var protectedValue = protector.Protect(probeText);
        var unprotectedValue = protector.Unprotect(protectedValue);
        if (!string.Equals(probeText, unprotectedValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The configured Data Protection key ring failed its startup round-trip check.");
        }

    }
}