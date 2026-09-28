using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Domain;

public class MeetingMinutesWorkItemLink : BaseEntity
{
    public Guid MeetingMinutesId { get; set; }
    public MeetingMinutes MeetingMinutes { get; set; } = null!;
    public Guid WorkItemId { get; set; }
}
