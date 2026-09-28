using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Domain;

public sealed class MeetingMinutesAttachment : BaseEntity
{
    public Guid MeetingMinutesId { get; set; }
    public MeetingMinutes MeetingMinutes { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string StoragePath { get; set; } = string.Empty;
}
