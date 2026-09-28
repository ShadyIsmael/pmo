using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Domain;

public sealed class MeetingReminderDispatch : BaseEntity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;
    public DateTime ScheduledAtUtc { get; set; }
    public string Status { get; set; } = MeetingReminderDispatchStatuses.Pending;
}
