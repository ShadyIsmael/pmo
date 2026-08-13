using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Domain;

public class MeetingMinutes : BaseEntity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;
    public string? Content { get; set; }
    public string? Decisions { get; set; }
    public string? DiscussionPoints { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public Guid? RecordedBy { get; set; }
}
