using NEXUS.Modules.Documents.Infrastructure;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class LocalStorageProviderPathTraversalTests
{
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("nested/../../outside.txt")]
    public async Task GetAsync_rejects_keys_that_escape_the_storage_root(string key)
    {
        var provider = new LocalStorageProvider(Directory.GetCurrentDirectory());

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync(key));
    }

    [Fact]
    public async Task SaveAsync_rejects_a_folder_that_escapes_the_storage_root()
    {
        var provider = new LocalStorageProvider(Directory.GetCurrentDirectory());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.SaveAsync("payload.txt", Array.Empty<byte>(), ".."));
    }

    [Fact]
    public async Task GetAsync_rejects_a_case_variant_alias_after_parent_traversal()
    {
        var basePath = Directory.GetCurrentDirectory();
        var directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(basePath));
        var caseVariant = new string(directoryName.Select(character =>
            char.IsUpper(character) ? char.ToLowerInvariant(character) :
            char.IsLower(character) ? char.ToUpperInvariant(character) :
            character).ToArray());
        if (string.Equals(directoryName, caseVariant, StringComparison.Ordinal))
            return;

        var key = Path.Combine("..", caseVariant, "outside.txt");
        var provider = new LocalStorageProvider(basePath);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync(key));
    }
}
