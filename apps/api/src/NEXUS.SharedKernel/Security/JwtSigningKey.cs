using System.Security.Cryptography;
using System.Text;

namespace NEXUS.SharedKernel.Security;

/// <summary>
/// Resolves and validates the symmetric key used to sign and validate JWT access tokens.
/// No fallback signing key is compiled into the application: a configured secret is required
/// everywhere except Development, where the host generates a process-local ephemeral key.
/// </summary>
public static class JwtSigningKey
{
    public const string ConfigurationKey = "Jwt:Secret";

    /// <summary>Minimum UTF-8 key length accepted for HS256 signing.</summary>
    public const int MinimumKeyBytes = 32;

    /// <summary>
    /// Returns the configured signing secret or throws when it is absent or too short.
    /// </summary>
    public static string Require(string? configuredSecret)
    {
        if (string.IsNullOrWhiteSpace(configuredSecret))
        {
            throw new InvalidOperationException(
                "Jwt:Secret is not configured. Supply a high-entropy signing secret from a secret store " +
                "(for example the Jwt__Secret environment variable) before starting the application.");
        }

        if (Encoding.UTF8.GetByteCount(configuredSecret) < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:Secret must be at least {MinimumKeyBytes} bytes of UTF-8 data to sign HS256 tokens securely.");
        }

        return configuredSecret;
    }

    /// <summary>
    /// Creates a cryptographically random signing secret. Used only for Development so that no
    /// secret value ever lives in source control; tokens signed with it do not survive a restart.
    /// </summary>
    public static string CreateEphemeral()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}
