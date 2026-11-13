using System.Buffers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NEXUS.Modules.Documents.Application;

namespace NEXUS.Modules.Documents.Infrastructure;

/// <summary>
/// Local Development storage. Physical files are named with random identifiers and remain below
/// the configured private root; no user filename or document path is ever used as a filesystem path.
/// </summary>
public sealed class LocalStorageProvider : IStorageProvider
{
    public const string Identifier = "local";
    private static readonly Regex ExtensionPattern = new(@"^\.[A-Za-z0-9]{1,10}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex StorageKeyPattern = new(@"^[a-fA-F0-9]{32}(?:\.[A-Za-z0-9]{1,10})?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LegacyStorageKeyPattern = new(@"^[a-fA-F0-9]{32}/[a-fA-F0-9]{32}/v[1-9][0-9]*-[a-fA-F0-9]{32}\.(?:pdf|docx|xlsx|pptx|png|jpe?g|txt|csv)$", RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LegacyAttachmentStorageKeyPattern = new(@"^(?:meeting-minutes|work-items)/[a-fA-F0-9]{32}/[a-fA-F0-9]{32}/[a-fA-F0-9]{32}\.[A-Za-z0-9]{1,10}$", RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly string _basePath;

    public string ProviderId => Identifier;

    // Concrete compatibility members preserve callers typed as LocalStorageProvider directly.
    public Task<string> SaveAsync(string fileName, byte[] content, string? folder = null, CancellationToken cancellationToken = default)
        => StorageProviderCompatibility.SaveAsync(this, fileName, content, folder, cancellationToken);

    public Task<byte[]> GetAsync(string storageKey, CancellationToken cancellationToken = default)
        => StorageProviderCompatibility.GetAsync(this, storageKey, cancellationToken);

    public LocalStorageProvider(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
            throw new ArgumentException("A private document storage root is required.", nameof(basePath));

        _basePath = Path.GetFullPath(basePath);
        if (IsUnderWebRoot(_basePath))
            throw new ArgumentException("Document storage must be outside every wwwroot directory.", nameof(basePath));

        EnsureNoReparsePointsAlongPath(_basePath);
        Directory.CreateDirectory(_basePath);
        EnsureNoReparsePointsAlongPath(_basePath);
    }

    public async Task<StoredFile> StoreAsync(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead) throw new ArgumentException("The supplied content stream must be readable.", nameof(content));
        var safeExtension = ValidateExtension(extension);
        EnsureNoReparsePointsAlongPath(_basePath);

        var storageKey = $"{Guid.NewGuid():N}{safeExtension}";
        var destination = ResolvePath(storageKey);
        var temporaryKey = $".pending-{Guid.NewGuid():N}";
        var temporaryPath = Path.Combine(_basePath, temporaryKey);
        using var checksum = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        long length = 0;

        try
        {
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                int read;
                while ((read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    checksum.AppendData(buffer, 0, read);
                    length += read;
                }

                await output.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            EnsureNoReparsePointsAlongPath(_basePath);
            File.Move(temporaryPath, destination, overwrite: false);
            var hash = Convert.ToHexString(checksum.GetHashAndReset()).ToLowerInvariant();
            return new StoredFile(ProviderId, storageKey, length, hash);
        }
        catch (UnauthorizedAccessException)
        {
            TryDelete(temporaryPath);
            throw new IOException("Document content could not be stored.");
        }
        catch (IOException)
        {
            TryDelete(temporaryPath);
            throw new IOException("Document content could not be stored.");
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var path = ResolvePath(storageKey);
            if (!File.Exists(path)) throw new FileNotFoundException("Stored document content was not found.");
            RejectReparsePoint(path);

            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return Task.FromResult(stream);
        }
        catch (FileNotFoundException)
        {
            throw new FileNotFoundException("Stored document content was not found.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new IOException("Stored document content could not be read.");
        }
        catch (IOException)
        {
            throw new IOException("Stored document content could not be read.");
        }
    }

    public async Task<StoredFileMetadata?> GetMetadataAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = ResolvePath(storageKey);
            if (!File.Exists(path)) return null;
            RejectReparsePoint(path);

            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var checksum = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                int read;
                while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                    checksum.AppendData(buffer, 0, read);

                return new StoredFileMetadata(ProviderId, storageKey, stream.Length, Convert.ToHexString(checksum.GetHashAndReset()).ToLowerInvariant());
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            throw new IOException("Stored document metadata could not be read.");
        }
        catch (IOException)
        {
            throw new IOException("Stored document metadata could not be read.");
        }
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(storageKey);
        if (!File.Exists(path)) return Task.FromResult(false);
        RejectReparsePoint(path);
        return Task.FromResult(true);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var path = ResolvePath(storageKey);
            if (File.Exists(path))
            {
                RejectReparsePoint(path);
                File.Delete(path);
            }

            return Task.CompletedTask;
        }
        catch (UnauthorizedAccessException)
        {
            throw new IOException("Stored document content could not be deleted.");
        }
        catch (IOException)
        {
            throw new IOException("Stored document content could not be deleted.");
        }
    }

    private string ResolvePath(string storageKey)
    {
        if (!IsValidStorageKey(storageKey))
            throw new InvalidOperationException("Storage key is invalid.");

        // Old document rows used relative keys shaped as {owner}/{document}/vN-{guid}.ext.
        // Accept only that historical format while all new writes remain flat opaque keys.
        var normalizedKey = storageKey.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_basePath, normalizedKey));
        var baseWithSeparator = Path.EndsInDirectorySeparator(_basePath) ? _basePath : _basePath + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(baseWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Storage key escapes the configured storage root.");

        return fullPath;
    }

    private static bool IsValidStorageKey(string storageKey)
        => !string.IsNullOrWhiteSpace(storageKey)
            && (StorageKeyPattern.IsMatch(storageKey)
                || LegacyStorageKeyPattern.IsMatch(storageKey)
                || LegacyAttachmentStorageKeyPattern.IsMatch(storageKey));

    private static string ValidateExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension) || !ExtensionPattern.IsMatch(extension))
            throw new ArgumentException("A safe file extension is required.", nameof(extension));
        return extension.ToLowerInvariant();
    }

    private static bool IsUnderWebRoot(string path) => path
        .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
        .Any(segment => string.Equals(segment, "wwwroot", StringComparison.OrdinalIgnoreCase));

    private void RejectReparsePoint(string path)
    {
        try
        {
            var currentDirectory = Path.GetDirectoryName(path);
            while (currentDirectory is not null)
            {
                if (Directory.Exists(currentDirectory)
                    && (File.GetAttributes(currentDirectory) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Stored document content cannot pass through a filesystem link.");

                if (string.Equals(Path.TrimEndingDirectorySeparator(currentDirectory), _basePath, StringComparison.OrdinalIgnoreCase))
                    break;

                currentDirectory = Path.GetDirectoryName(currentDirectory);
            }

            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Stored document content cannot be a filesystem link.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new IOException("Stored document access could not be validated.");
        }
        catch (IOException)
        {
            throw new IOException("Stored document access could not be validated.");
        }
    }

    private static void EnsureNoReparsePointsAlongPath(string path)
    {
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
        {
            try
            {
                if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Configured document storage cannot use filesystem links.");
            }
            catch (UnauthorizedAccessException)
            {
                throw new InvalidOperationException("Configured document storage root cannot be accessed.");
            }
            catch (IOException)
            {
                throw new InvalidOperationException("Configured document storage root cannot be accessed.");
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Preserve the original storage failure. A temp file is never returned to a caller.
        }
    }
}
