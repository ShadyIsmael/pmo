using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace NEXUS.Modules.Integrations.Infrastructure;

public interface IIntegrationSecretProtector
{
    /// <summary>
    /// Protects a secret for at-rest storage. Returns null for null/empty input.
    /// </summary>
    string? Protect(string? plaintext);

    /// <summary>
    /// Attempts to obtain usable plaintext for an integration secret. Returns false when
    /// the value is not protected or cannot be unprotected (for example, because the key
    /// ring is unavailable or the value is corrupt). Legacy plaintext is deliberately
    /// rejected until it has been migrated through a reviewed data-migration path.
    /// </summary>
    bool TryUnprotect(string? storedValue, out string? plaintext);

    /// <summary>
    /// Returns true when the stored value uses this protector's ciphertext marker.
    /// </summary>
    bool IsProtected(string? storedValue);
}

internal sealed class IntegrationSecretProtector : IIntegrationSecretProtector
{
    internal const string ProtectedPrefix = "NEXUSINTDP1:";
    private readonly IDataProtector _protector;

    public IntegrationSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("NEXUS.Modules.Integrations.Secrets.v1");
    }

    public string? Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext;
        if (IsProtected(plaintext))
        {
            if (TryUnprotect(plaintext, out _)) return plaintext;
            throw new CryptographicException("The integration secret is marked as protected but cannot be verified.");
        }

        return ProtectedPrefix + _protector.Protect(plaintext);
    }

    public bool IsProtected(string? storedValue) =>
        !string.IsNullOrEmpty(storedValue) && storedValue.StartsWith(ProtectedPrefix, StringComparison.Ordinal);

    public bool TryUnprotect(string? storedValue, out string? plaintext)
    {
        if (string.IsNullOrEmpty(storedValue))
        {
            plaintext = storedValue;
            return true;
        }

        if (!IsProtected(storedValue))
        {
            // Fail closed for legacy plaintext. P0-07C must re-encrypt existing values
            // using the configured durable key ring before secret-consuming features run.
            plaintext = null;
            return false;
        }

        var payload = storedValue[ProtectedPrefix.Length..];
        if (payload.Length == 0)
        {
            plaintext = null;
            return false;
        }

        try
        {
            plaintext = _protector.Unprotect(payload);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            plaintext = null;
            return false;
        }
    }
}