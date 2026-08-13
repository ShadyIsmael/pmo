namespace NEXUS.Modules.Documents.Application;

public interface IStorageProvider
{
    Task<string> SaveAsync(string fileName, byte[] content, string? folder = null, CancellationToken cancellationToken = default);
    Task<byte[]> GetAsync(string path, CancellationToken cancellationToken = default);
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}
