namespace NEXUS.Modules.Identity.Application;

public sealed class LocalPasswordPolicyOptions
{
    public const string SectionName = "Security:LocalPasswords";

    public int TemporaryPasswordExpiryHours { get; set; } = 24;
    public int MinimumLength { get; set; } = 12;
    public int MaximumLength { get; set; } = 128;
}
