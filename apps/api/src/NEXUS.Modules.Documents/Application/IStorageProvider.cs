namespace NEXUS.Modules.Documents.Application;

/// <summary>
/// Stores document bytes without coupling document metadata to a physical filesystem path.
/// </summary>
public interface IStorageProvider
{
    /// <summary>Stable provider identifier persisted with the document version.</summary>
    string ProviderId { get; }

    /// <summary>
    /// Stores the supplied stream under a newly generated opaque key. The extension must already
    /// have been validated by the document layer; providers still validate it before use.
    /// </summary>
    Task<StoredFile> StoreAsync(Stream content, string extension, CancellationToken cancellationToken = default);

    /// <summary>Opens a stored object for reading. Missing objects raise <see cref="FileNotFoundException"/>.</summary>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>Returns provider metadata for the object, or null when the object does not exist.</summary>
    Task<StoredFileMetadata?> GetMetadataAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>Checks whether an object exists without disclosing its physical location.</summary>
    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>Deletes the object. Deleting an already absent object is safe.</summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);

    // Compatibility adapters for existing meeting and work-item attachment services. They
    // deliberately discard caller paths and filenames: only a safe extension is carried forward.
    Task<string> SaveAsync(string fileName, byte[] content, string? folder = null, CancellationToken cancellationToken = default)
        => StorageProviderCompatibility.SaveAsync(this, fileName, content, folder, cancellationToken);

    Task<byte[]> GetAsync(string storageKey, CancellationToken cancellationToken = default)
        => StorageProviderCompatibility.GetAsync(this, storageKey, cancellationToken);
}

public static class StorageProviderCompatibility
{
    public static async Task<string> SaveAsync(IStorageProvider provider, string fileName, byte[] content, string? folder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(content);
        ValidateLegacyFolder(folder);
        var extension = Path.GetExtension(fileName ?? string.Empty);
        await using var stream = new MemoryStream(content, writable: false);
        var stored = await provider.StoreAsync(stream, extension, cancellationToken);
        return stored.StorageKey;
    }

    public static async Task<byte[]> GetAsync(IStorageProvider provider, string storageKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        await using var input = await provider.OpenReadAsync(storageKey, cancellationToken);
        await using var output = new MemoryStream();
        await input.CopyToAsync(output, cancellationToken);
        return output.ToArray();
    }

    private static void ValidateLegacyFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return;
        if (Path.IsPathRooted(folder)
            || folder.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment is "." or ".." || segment.Contains(':')))
            throw new InvalidOperationException("Storage folder must be a safe relative value.");
    }
}

public sealed record StoredFile(string ProviderId, string StorageKey, long Length, string Sha256);

public sealed record StoredFileMetadata(string ProviderId, string StorageKey, long Length, string Sha256);
