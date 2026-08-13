using NEXUS.SharedKernel;

namespace NEXUS.Modules.Notifications.Domain;

public class NotificationTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? BodyTemplate { get; set; }
    public string? Channel { get; set; }
    public bool IsActive { get; set; } = true;
}
