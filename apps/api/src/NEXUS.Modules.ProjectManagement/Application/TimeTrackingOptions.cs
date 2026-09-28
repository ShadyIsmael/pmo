namespace NEXUS.Modules.ProjectManagement.Application;

public sealed class TimeTrackingOptions
{
    public int IdleMinutes { get; set; } = 15;
    public int HeartbeatSeconds { get; set; } = 30;
}
