using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Domain;

public class MeetingMinutes : BaseEntity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;
    public string? Summary { get; set; }
    public string? Decisions { get; set; }
    public string? DiscussionNotes { get; set; }
    public string? Outcomes { get; set; }
    public string State { get; set; } = MeetingMinutesStates.Draft;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public Guid? RecordedBy { get; set; }
    public ICollection<MeetingMinutesComment> Comments { get; set; } = new List<MeetingMinutesComment>();
    public ICollection<MeetingMinutesChecklistItem> ChecklistItems { get; set; } = new List<MeetingMinutesChecklistItem>();
    public ICollection<MeetingMinutesAttachment> Attachments { get; set; } = new List<MeetingMinutesAttachment>();
    public ICollection<MeetingMinutesWorkItemLink> WorkItemLinks { get; set; } = new List<MeetingMinutesWorkItemLink>();
}
