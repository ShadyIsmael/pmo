namespace NEXUS.Modules.Notifications.Application;

public sealed record NotificationPreferenceDto(
    string Type,
    bool InAppEnabled,
    bool PushEnabled,
    bool EmailEnabled);

public sealed record NotificationPreferencesResponse(
    IReadOnlyList<NotificationPreferenceDto> Items,
    bool PushAvailable,
    bool EmailAvailable);

public sealed record UpdateNotificationPreferencesRequest(
    IReadOnlyList<NotificationPreferenceDto> Items);
