namespace NEXUS.Modules.Integrations.Infrastructure;

public sealed class DirectoryLdapOptions
{
    public const string SectionName = "DirectoryProviders:Ldap";

    public string[] AllowedHosts { get; set; } = [];
}
