using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Domain;

public sealed class MeetingMinutesChecklistItem : BaseEntity
{
    public Guid MeetingMinutesId { get; set; }
    public MeetingMinutes MeetingMinutes { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
}
