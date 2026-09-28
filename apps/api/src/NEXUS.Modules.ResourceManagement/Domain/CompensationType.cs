namespace NEXUS.Modules.ResourceManagement.Domain;

public static class CompensationType
{
    public const string Hourly = "Hourly";
    public const string Weekly = "Weekly";
    public const string Monthly = "Monthly";

    public static bool IsValid(string? value) => value is Hourly or Weekly or Monthly;
}
