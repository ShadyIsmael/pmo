using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Meetings.Application;

namespace NEXUS.Persistence;

public sealed class MeetingMinutesAttachmentStorage : IMeetingMinutesAttachmentStorage
{
    private readonly IStorageProvider _storageProvider;

    public MeetingMinutesAttachmentStorage(IStorageProvider storageProvider) => _storageProvider = storageProvider;

    public Task<string> SaveAsync(string fileName, byte[] content, string folder, CancellationToken cancellationToken = default) => _storageProvider.SaveAsync(fileName, content, folder, cancellationToken);
    public Task<byte[]> GetAsync(string path, CancellationToken cancellationToken = default) => _storageProvider.GetAsync(path, cancellationToken);
    public Task DeleteAsync(string path, CancellationToken cancellationToken = default) => _storageProvider.DeleteAsync(path, cancellationToken);
}
