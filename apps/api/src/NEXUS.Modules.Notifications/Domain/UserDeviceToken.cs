using NEXUS.SharedKernel;

namespace NEXUS.Modules.Notifications.Domain;

public sealed class UserDeviceToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeactivatedAt { get; set; }
}
