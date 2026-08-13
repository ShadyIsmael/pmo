using NEXUS.SharedKernel;

namespace NEXUS.Modules.Notifications.Domain;

public class NotificationPreference : BaseEntity
{
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool EmailEnabled { get; set; } = true;
    public bool InAppEnabled { get; set; } = true;
    public bool PushEnabled { get; set; }
    public bool DigestEnabled { get; set; }
    public string? DigestFrequency { get; set; }
}
