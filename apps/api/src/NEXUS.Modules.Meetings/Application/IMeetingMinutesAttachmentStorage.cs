namespace NEXUS.Modules.Meetings.Application;

public interface IMeetingMinutesAttachmentStorage
{
    Task<string> SaveAsync(string fileName, byte[] content, string folder, CancellationToken cancellationToken = default);
    Task<byte[]> GetAsync(string path, CancellationToken cancellationToken = default);
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}
