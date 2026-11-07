using NEXUS.SharedKernel;

namespace NEXUS.Modules.Notifications.Domain;

public enum NotificationDeliveryChannel
{
    Push = 1,
    Email = 2,
}

public enum NotificationDeliveryStatus
{
    Pending = 1,
    Processing = 2,
    Delivered = 3,
    RetryScheduled = 4,
    FailedPermanent = 5,
    Cancelled = 6,
    /// <summary>The provider accepted the email submission; this is not a recipient delivery receipt.</summary>
    Accepted = 7,
}

public enum NotificationAttemptStatus
{
    Pending = 1,
    Processing = 2,
    Delivered = 3,
    RetryScheduled = 4,
    FailedPermanent = 5,
    Cancelled = 6,
}

public sealed class NotificationDelivery : BaseEntity
{
    public Guid NotificationId { get; set; }
    public NotificationDeliveryChannel Channel { get; set; }
    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? FailureCategory { get; set; }
    public string? LastError { get; set; }
}

/// <summary>One independently retried device attempt under a notification/channel delivery.</summary>
public sealed class NotificationDeliveryAttempt : BaseEntity
{
    public Guid NotificationDeliveryId { get; set; }
    public Guid DeviceTokenId { get; set; }
    public NotificationAttemptStatus Status { get; set; } = NotificationAttemptStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? FailureCategory { get; set; }
    public string? LastError { get; set; }
}
