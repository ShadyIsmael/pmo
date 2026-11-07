using System.Text;
using NEXUS.SharedKernel.Security;
using Xunit;

namespace NEXUS.Api.Tests.Security;

/// <summary>
/// Regression coverage for the JWT signing-key policy: the application must never fall back to a
/// compiled-in secret, and it must reject short keys before they reach the token signer.
/// </summary>
public class JwtSigningKeyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("too-short-secret")]
    public void Require_rejects_missing_or_short_secrets(string? configuredSecret)
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            JwtSigningKey.Require(configuredSecret);
        });
    }

    [Fact]
    public void Require_accepts_a_secret_at_the_byte_floor()
    {
        var secret = new string('k', JwtSigningKey.MinimumKeyBytes);

        Assert.Equal(secret, JwtSigningKey.Require(secret));
    }

    [Fact]
    public void Require_measures_utf8_bytes_rather_than_characters()
    {
        // Arabic characters are two UTF-8 bytes each: 15 characters (30 bytes) must fail,
        // 16 characters (32 bytes) must pass.
        var tooShort = new string('\u0645', 15);
        var accepted = new string('\u0645', 16);

        Assert.Equal(30, Encoding.UTF8.GetByteCount(tooShort));
        Assert.Throws<InvalidOperationException>(() => JwtSigningKey.Require(tooShort));
        Assert.Equal(accepted, JwtSigningKey.Require(accepted));
    }

    [Fact]
    public void CreateEphemeral_produces_distinct_strong_keys()
    {
        var first = JwtSigningKey.CreateEphemeral();
        var second = JwtSigningKey.CreateEphemeral();

        Assert.NotEqual(first, second);
        Assert.True(Encoding.UTF8.GetByteCount(first) >= JwtSigningKey.MinimumKeyBytes);
        Assert.Equal(first, JwtSigningKey.Require(first));
    }
}
