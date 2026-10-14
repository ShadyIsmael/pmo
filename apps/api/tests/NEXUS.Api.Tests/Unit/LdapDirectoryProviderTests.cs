using Microsoft.Extensions.Options;
using NEXUS.Modules.Integrations.Domain;
using NEXUS.Modules.Integrations.Infrastructure;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class LdapDirectoryProviderTests
{
    [Fact]
    public async Task TestConnection_RejectsHostNotInApiAllowlist()
    {
        var provider = new LdapDirectoryProvider(Options.Create(new DirectoryLdapOptions
        {
            AllowedHosts = ["directory.example.test"]
        }));
        var connection = new DirectoryConnection
        {
            Host = "unlisted.example.test",
            Port = 636,
            UseSsl = true,
            BaseDn = "dc=example,dc=test",
            BindUser = "cn=directory-reader,dc=example,dc=test"
        };

        var result = await provider.TestConnectionAsync(connection, "test-only-credential");

        Assert.False(result.Connected);
        Assert.Equal("LDAP_HOST_NOT_ALLOWLISTED", result.ErrorCode);
    }

    [Fact]
    public async Task TestConnection_RejectsAllHostsWhenAllowlistIsNotConfigured()
    {
        var provider = new LdapDirectoryProvider(Options.Create(new DirectoryLdapOptions()));
        var connection = new DirectoryConnection
        {
            Host = "directory.example.test",
            Port = 636,
            UseSsl = true,
            BaseDn = "dc=example,dc=test",
            BindUser = "cn=directory-reader,dc=example,dc=test"
        };

        var result = await provider.TestConnectionAsync(connection, "test-only-credential");

        Assert.False(result.Connected);
        Assert.Equal("LDAP_HOST_NOT_ALLOWLISTED", result.ErrorCode);
    }
}
