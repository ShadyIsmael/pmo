namespace NEXUS.Modules.Identity.Application;

/// <summary>
/// The categories tenant administrators may assign to custom roles. System is a
/// reserved category for protected, code-defined roles and is not a selectable
/// custom-role category.
/// </summary>
public static class RoleCategories
{
    public const string Tenant = "Tenant";
    public const string Organization = "Organization";
    public const string Management = "Management";
    public const string Operation = "Operation";
    public const string Business = "Business";
    public const string System = "System";

    public static IReadOnlyList<string> Selectable { get; } = Array.AsReadOnly(
        new[] { Tenant, Organization, Management, Operation, Business });

    public static bool IsSelectable(string? category) =>
        category is not null && Selectable.Contains(category, StringComparer.Ordinal);
}
