using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Domain;

public class MeetingAgenda : BaseEntity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;
    public string Topic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public int? DurationMinutes { get; set; }
    public Guid? OwnerId { get; set; }
}
