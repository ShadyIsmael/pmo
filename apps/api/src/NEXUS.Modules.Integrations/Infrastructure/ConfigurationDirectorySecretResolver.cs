using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using NEXUS.Modules.Integrations.Application;

namespace NEXUS.Modules.Integrations.Infrastructure;

public sealed class ConfigurationDirectorySecretResolver(IConfiguration configuration) : IDirectorySecretResolver
{
    private static readonly Regex SafeReference = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string? Resolve(Guid tenantId, Guid connectionId, string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || !SafeReference.IsMatch(reference)) return null;
        var value = configuration[$"DirectorySecrets:{tenantId:D}:{connectionId:D}:{reference}"];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
