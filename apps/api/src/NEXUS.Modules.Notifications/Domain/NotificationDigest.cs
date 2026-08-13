using NEXUS.SharedKernel;

namespace NEXUS.Modules.Notifications.Domain;

public class NotificationDigest : BaseEntity
{
    public Guid UserId { get; set; }
    public string? Email { get; set; }
    public string Frequency { get; set; } = string.Empty;
    public DateTime? LastSentAt { get; set; }
    public DateTime? NextScheduledAt { get; set; }
}
