namespace NEXUS.Modules.Notifications.Application;

public sealed record NotificationChannelEligibility(
    bool InAppEnabled,
    bool PushEnabled,
    bool EmailEnabled,
    bool PushAvailable,
    bool EmailAvailable);
