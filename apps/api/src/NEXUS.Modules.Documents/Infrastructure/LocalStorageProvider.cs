using NEXUS.Modules.Documents.Application;

namespace NEXUS.Modules.Documents.Infrastructure;

public class LocalStorageProvider : IStorageProvider
{
    private readonly string _basePath;

    public LocalStorageProvider(string basePath)
    {
        _basePath = Path.GetFullPath(basePath);
        Directory.CreateDirectory(_basePath);
    }

    public Task<string> SaveAsync(string fileName, byte[] content, string? folder = null, CancellationToken cancellationToken = default)
    {
        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName != fileName)
            throw new InvalidOperationException("Storage file name is invalid.");

        var path = ResolvePath(folder is null ? safeFileName : Path.Combine(folder, safeFileName));
        var dir = Path.GetDirectoryName(path);
        if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, content);
        return Task.FromResult(Path.GetRelativePath(_basePath, path).Replace('\\', '/'));
    }

    public Task<byte[]> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.ReadAllBytes(ResolvePath(path)));
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var resolved = ResolvePath(path);
        if (File.Exists(resolved)) File.Delete(resolved);
        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || Path.IsPathRooted(key))
            throw new InvalidOperationException("Storage key must be a relative opaque key.");

        var fullPath = Path.GetFullPath(Path.Combine(_basePath, key));
        var baseWithSeparator = _basePath.EndsWith(Path.DirectorySeparatorChar)
            ? _basePath
            : _basePath + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(baseWithSeparator, StringComparison.Ordinal))
            throw new InvalidOperationException("Storage key escapes the configured storage root.");
        return fullPath;
    }
}
