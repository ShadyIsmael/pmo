using System.Security.Cryptography;
using System.Text;
using NEXUS.Modules.Documents.Infrastructure;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class LocalStorageProviderContractTests
{
    [Fact]
    public async Task Store_returns_an_opaque_key_and_provider_neutral_metadata_and_supports_streaming_read()
    {
        using var root = new TemporaryDirectory();
        var provider = new LocalStorageProvider(root.Path);
        var bytes = Encoding.UTF8.GetBytes("approved template contents");

        var stored = await provider.StoreAsync(new MemoryStream(bytes), ".PDF");

        Assert.Equal(LocalStorageProvider.Identifier, stored.ProviderId);
        Assert.Matches("^[a-f0-9]{32}\\.pdf$", stored.StorageKey);
        Assert.Equal(bytes.Length, stored.Length);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), stored.Sha256);
        Assert.DoesNotContain("template", stored.StorageKey, StringComparison.OrdinalIgnoreCase);

        await using var readStream = await provider.OpenReadAsync(stored.StorageKey);
        using var output = new MemoryStream();
        await readStream.CopyToAsync(output);
        Assert.Equal(bytes, output.ToArray());
    }

    [Fact]
    public async Task Metadata_existence_and_delete_are_consistent()
    {
        using var root = new TemporaryDirectory();
        var provider = new LocalStorageProvider(root.Path);
        var stored = await provider.StoreAsync(new MemoryStream([1, 2, 3, 4]), ".txt");

        Assert.True(await provider.ExistsAsync(stored.StorageKey));
        var metadata = await provider.GetMetadataAsync(stored.StorageKey);
        Assert.NotNull(metadata);
        Assert.Equal(stored.ProviderId, metadata.ProviderId);
        Assert.Equal(stored.StorageKey, metadata.StorageKey);
        Assert.Equal(stored.Length, metadata.Length);
        Assert.Equal(stored.Sha256, metadata.Sha256);

        await provider.DeleteAsync(stored.StorageKey);
        await provider.DeleteAsync(stored.StorageKey);

        Assert.False(await provider.ExistsAsync(stored.StorageKey));
        Assert.Null(await provider.GetMetadataAsync(stored.StorageKey));
        await Assert.ThrowsAsync<FileNotFoundException>(() => provider.OpenReadAsync(stored.StorageKey));
    }

    [Fact]
    public async Task Existing_relative_document_keys_remain_readable()
    {
        using var root = new TemporaryDirectory();
        var provider = new LocalStorageProvider(root.Path);
        var legacyKey = $"{Guid.NewGuid():N}/{Guid.NewGuid():N}/v1-{Guid.NewGuid():N}.pdf";
        var bytes = Encoding.UTF8.GetBytes("existing document");
        var physicalPath = Path.Combine(root.Path, legacyKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        await File.WriteAllBytesAsync(physicalPath, bytes);

        var result = await provider.GetAsync(legacyKey);

        Assert.Equal(bytes, result);
    }

    [Fact]
    public async Task Legacy_key_validation_rejects_relative_paths_outside_the_historical_document_shape()
    {
        using var root = new TemporaryDirectory();
        var provider = new LocalStorageProvider(root.Path);
        var invalidLegacyKey = $"{Guid.NewGuid():N}/{Guid.NewGuid():N}/outside.txt";

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.OpenReadAsync(invalidLegacyKey));
    }

    [Theory]
    [InlineData("meeting-minutes")]
    [InlineData("work-items")]
    public async Task Existing_meeting_and_work_item_attachment_keys_remain_readable_and_deletable(string bucket)
    {
        using var root = new TemporaryDirectory();
        var provider = new LocalStorageProvider(root.Path);
        var legacyKey = $"{bucket}/{Guid.NewGuid():N}/{Guid.NewGuid():N}/{Guid.NewGuid():N}.bin";
        var bytes = Encoding.UTF8.GetBytes("existing attachment");
        var physicalPath = Path.Combine(root.Path, legacyKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        await File.WriteAllBytesAsync(physicalPath, bytes);

        Assert.True(await provider.ExistsAsync(legacyKey));
        Assert.Equal(bytes, await provider.GetAsync(legacyKey));
        Assert.NotNull(await provider.GetMetadataAsync(legacyKey));

        await provider.DeleteAsync(legacyKey);

        Assert.False(await provider.ExistsAsync(legacyKey));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../pdf")]
    [InlineData(".pdf/")]
    [InlineData(".pdf.exe")]
    public async Task Store_rejects_an_invalid_extension(string extension)
    {
        using var root = new TemporaryDirectory();
        var provider = new LocalStorageProvider(root.Path);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.StoreAsync(new MemoryStream([1]), extension));
    }

    [Fact]
    public void Constructor_rejects_storage_under_a_web_root()
    {
        using var root = new TemporaryDirectory();

        var exception = Assert.Throws<ArgumentException>(() =>
            new LocalStorageProvider(Path.Combine(root.Path, "wwwroot", "documents")));

        Assert.Contains("outside", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_rejects_a_configured_root_below_a_reparse_point()
    {
        using var root = new TemporaryDirectory();
        var target = Path.Combine(root.Path, "target");
        var link = Path.Combine(root.Path, "linked-parent");
        Directory.CreateDirectory(target);
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            // Windows hosts without Developer Mode or symlink privilege cannot exercise this case.
            return;
        }

        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                new LocalStorageProvider(Path.Combine(link, "documents")));
        }
        finally
        {
            if (Directory.Exists(link)) Directory.Delete(link);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nexus-storage-tests", Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
