namespace NEXUS.Modules.Audit.Application;

/// <summary>
/// Identifies who or what caused an audited change. System-generated changes
/// must stay distinguishable from human actions.
/// </summary>
public static class AuditSources
{
    public const string User = "User";
    public const string System = "System";
    public const string Integration = "Integration";
    public const string BackgroundJob = "BackgroundJob";
    public const string Unknown = "Unknown";

    public static bool IsKnown(string? source) =>
        source is User or System or Integration or BackgroundJob;

    public static string Normalize(string? source) =>
        IsKnown(source) ? source! : Unknown;
}
