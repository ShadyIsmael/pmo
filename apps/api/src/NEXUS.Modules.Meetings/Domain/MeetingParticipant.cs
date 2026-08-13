using NEXUS.SharedKernel;

namespace NEXUS.Modules.Meetings.Domain;

public class MeetingParticipant : BaseEntity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AttendanceStatus { get; set; }
    public string? Role { get; set; }
    public DateTime? RespondedAt { get; set; }
}
