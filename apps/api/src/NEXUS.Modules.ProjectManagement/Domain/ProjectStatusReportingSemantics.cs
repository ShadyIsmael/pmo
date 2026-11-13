namespace NEXUS.Modules.ProjectManagement.Domain;

public static class ProjectStatusReportingCategories
{
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Excluded = "Excluded";

    public static string? Normalize(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "INPROGRESS" => InProgress,
        "COMPLETED" => Completed,
        "EXCLUDED" => Excluded,
        _ => null
    };

    public static bool IsValid(string? value) => Normalize(value) is not null;

    public static string ForLegacyCode(string? normalizedCode) => normalizedCode?.Trim().ToUpperInvariant() switch
    {
        "COMPLETED" or "CLOSED" or "DONE" => Completed,
        "CANCELLED" or "CANCELED" or "ARCHIVED" => Excluded,
        _ => InProgress
    };
}

public static class ProjectStatusAttentionLevels
{
    public const string None = "None";
    public const string NeedsAttention = "NeedsAttention";
    public const string Critical = "Critical";

    public static string? Normalize(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "NONE" => None,
        "NEEDSATTENTION" => NeedsAttention,
        "CRITICAL" => Critical,
        _ => null
    };

    public static bool IsValid(string? value) => Normalize(value) is not null;

    public static string ForLegacyCode(string? normalizedCode) => normalizedCode?.Trim().ToUpperInvariant() switch
    {
        "CRITICAL" => Critical,
        "ON-HOLD" or "ONHOLD" or "RISK" or "DELAY" => NeedsAttention,
        _ => None
    };
}
