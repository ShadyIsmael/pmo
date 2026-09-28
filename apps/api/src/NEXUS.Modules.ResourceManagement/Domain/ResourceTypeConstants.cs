namespace NEXUS.Modules.ResourceManagement.Domain;

public static class ResourceTypeConstants
{
    public const string Staff = "Staff";
    public const string VendorResource = "VendorResource";

    public static bool IsValid(string? value) => value is Staff or VendorResource;
}
