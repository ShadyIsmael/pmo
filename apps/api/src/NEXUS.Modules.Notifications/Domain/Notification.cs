using NEXUS.SharedKernel;

namespace NEXUS.Modules.Notifications.Domain;

public class Notification : BaseEntity
{
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public bool IsInAppVisible { get; set; } = true;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? ScheduledAt { get; set; }
}
