namespace NEXUS.Modules.Notifications.Application;

public static class NotificationPreferenceDefaults
{
    public static bool InAppEnabled(string type) => true;
    public static bool PushEnabled(string type) => false;
    public static bool EmailEnabled(string type) => false;
}
