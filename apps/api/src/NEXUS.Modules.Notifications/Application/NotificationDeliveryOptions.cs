namespace NEXUS.Modules.Notifications.Application;

public sealed class NotificationDeliveryOptions
{
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 5;
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan ProcessingLease { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public void Validate()
    {
        if (BatchSize is < 1 or > 200) throw new InvalidOperationException("NotificationDelivery:BatchSize must be between 1 and 200.");
        if (MaxAttempts is < 1 or > 20) throw new InvalidOperationException("NotificationDelivery:MaxAttempts must be between 1 and 20.");
        if (InitialRetryDelay <= TimeSpan.Zero || MaxRetryDelay < InitialRetryDelay) throw new InvalidOperationException("Notification delivery retry delays are invalid.");
        if (PollInterval < TimeSpan.FromSeconds(5)) throw new InvalidOperationException("NotificationDelivery:PollInterval must be at least five seconds.");
        if (SendTimeout <= TimeSpan.Zero || ProcessingLease <= SendTimeout)
            throw new InvalidOperationException("NotificationDelivery:ProcessingLease must exceed a positive SendTimeout.");
    }
}

public static class NotificationRetryPolicy
{
    public static DateTime NextAttempt(DateTime now, int attemptCount, NotificationDeliveryOptions options)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, 20);
        var multiplier = Math.Pow(2, exponent);
        var ticks = Math.Min(options.InitialRetryDelay.Ticks * multiplier, options.MaxRetryDelay.Ticks);
        return now.AddTicks((long)ticks);
    }
}
