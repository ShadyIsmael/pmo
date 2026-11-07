namespace NEXUS.Modules.ResourceManagement.Domain;

/// <summary>
/// Stable API choices for resource classification. The database continues to store
/// the historical VendorResource code so existing records and integrations remain valid.
/// </summary>
public enum ResourceType
{
    Staff = 0,
    Vendor = 1,
    Outsourced = 2
}

public enum ResourceSeniorityLevel
{
    Junior = 0,
    Mid = 1,
    Senior = 2,
    Lead = 3,
    Principal = 4
}

public static class ResourceTypeConstants
{
    public const string Staff = "Staff";
    public const string VendorResource = "VendorResource";
    public const string Outsourced = "Outsourced";

    public static bool IsValid(string? value) => TryGetKnownStoredCode(value, out _);

    public static string ToStoredCode(ResourceType value) => value switch
    {
        ResourceType.Staff => Staff,
        ResourceType.Vendor => VendorResource,
        ResourceType.Outsourced => Outsourced,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown resource type")
    };

    public static string ToApiCode(string? storedValue)
    {
        var normalized = storedValue?.Trim();
        if (string.Equals(normalized, Staff, StringComparison.OrdinalIgnoreCase)) return nameof(ResourceType.Staff);
        if (string.Equals(normalized, VendorResource, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, nameof(ResourceType.Vendor), StringComparison.OrdinalIgnoreCase))
            return nameof(ResourceType.Vendor);
        if (string.Equals(normalized, Outsourced, StringComparison.OrdinalIgnoreCase)) return nameof(ResourceType.Outsourced);

        // Unknown historical values are returned unchanged for review rather than
        // silently being reclassified as Staff.
        return storedValue ?? string.Empty;
    }

    public static bool TryGetKnownStoredCode(string? value, out string storedCode)
    {
        if (string.Equals(value?.Trim(), Staff, StringComparison.OrdinalIgnoreCase))
        {
            storedCode = Staff;
            return true;
        }
        if (string.Equals(value?.Trim(), VendorResource, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value?.Trim(), nameof(ResourceType.Vendor), StringComparison.OrdinalIgnoreCase))
        {
            storedCode = VendorResource;
            return true;
        }
        if (string.Equals(value?.Trim(), Outsourced, StringComparison.OrdinalIgnoreCase))
        {
            storedCode = Outsourced;
            return true;
        }

        storedCode = string.Empty;
        return false;
    }
}
