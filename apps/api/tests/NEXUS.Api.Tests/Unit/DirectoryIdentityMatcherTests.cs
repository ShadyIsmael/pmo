using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Integrations.Application;
using NEXUS.Modules.Integrations.Domain;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class DirectoryIdentityMatcherTests
{
    [Fact]
    public void Match_ReturnsConflict_WhenDirectoryIdentifiersPointToDifferentUsers()
    {
        var connectionId = Guid.NewGuid();
        var linkedUserId = Guid.NewGuid();
        var emailUserId = Guid.NewGuid();
        var source = UserRecord("directory-id", "person@example.test", "person@example.test");
        var links = new[]
        {
            new DirectoryUserLink { DirectoryConnectionId = connectionId, ExternalDirectoryId = "directory-id", UserId = linkedUserId }
        };
        var users = new[]
        {
            new User { Id = emailUserId, Email = "person@example.test" }
        };

        var result = DirectoryIdentityMatcher.Match(connectionId, source, links, users);

        Assert.True(result.IsConflict);
        Assert.Null(result.UserId);
    }

    [Fact]
    public void Match_DoesNotUseDisplayNameAsIdentity()
    {
        var source = UserRecord("directory-id", "new@example.test", "new@example.test") with { DisplayName = "Alex Example" };
        var users = new[] { new User { Id = Guid.NewGuid(), FirstName = "Alex", LastName = "Example", Email = "other@example.test" } };

        var result = DirectoryIdentityMatcher.Match(Guid.NewGuid(), source, [], users);

        Assert.False(result.IsConflict);
        Assert.Null(result.UserId);
    }

    private static DirectoryUserRecord UserRecord(string id, string email, string upn) => new(
        id, upn, upn, "Directory User", email, null, null, null, null, true, [], new Dictionary<string, string?>());
}
