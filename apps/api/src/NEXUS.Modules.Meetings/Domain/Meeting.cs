using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Domain;

public class Meeting : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ProjectId { get; set; }
    public string? MeetingType { get; set; }
    public string? Location { get; set; }
    public string? MeetingLink { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Status { get; set; }
    public int? ReminderOffsetMinutes { get; set; }
    public string? Notes { get; set; }
    public Guid? OrganizerId { get; set; }
    public string? Recurrence { get; set; }
    public ICollection<MeetingAgenda> AgendaItems { get; set; } = new List<MeetingAgenda>();
    public ICollection<MeetingParticipant> Participants { get; set; } = new List<MeetingParticipant>();
    public ICollection<MeetingMinutes> Minutes { get; set; } = new List<MeetingMinutes>();
    public ICollection<MeetingAction> Actions { get; set; } = new List<MeetingAction>();
    public ICollection<MeetingReminderDispatch> ReminderDispatches { get; set; } = new List<MeetingReminderDispatch>();
}
