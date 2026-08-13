using NEXUS.Modules.Documents.Application;

namespace NEXUS.Modules.Documents.Infrastructure;

public class LocalStorageProvider : IStorageProvider
{
    private readonly string _basePath;

    public LocalStorageProvider(string basePath)
    {
        _basePath = basePath;
        if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);
    }

    public Task<string> SaveAsync(string fileName, byte[] content, string? folder = null, CancellationToken cancellationToken = default)
    {
        var path = folder is not null ? Path.Combine(_basePath, folder, fileName) : Path.Combine(_basePath, fileName);
        var dir = Path.GetDirectoryName(path);
        if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, content);
        return Task.FromResult(path);
    }

    public Task<byte[]> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.ReadAllBytes(path));
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
