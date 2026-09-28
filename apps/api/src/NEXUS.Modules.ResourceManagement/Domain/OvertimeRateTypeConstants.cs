namespace NEXUS.Modules.ResourceManagement.Domain;

public static class OvertimeRateTypeConstants
{
    public const string Multiplier = "Multiplier";
    public const string FixedHourlyRate = "FixedHourlyRate";

    public static bool IsValid(string? value) => value is Multiplier or FixedHourlyRate;
}
