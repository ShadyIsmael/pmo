namespace NEXUS.Modules.Identity.Application;

public sealed class LocalPasswordPolicyOptions
{
    public const string SectionName = "Security:LocalPasswords";

    public int TemporaryPasswordExpiryHours { get; set; } = 24;
    public int MinimumLength { get; set; } = 12;
    public int MaximumLength { get; set; } = 128;

    /// <summary>
    /// Allows only the seeded Development Admin to sign in despite initial-password
    /// state. The authentication boundary also requires the host to be Development.
    /// </summary>
    public bool DevelopmentAdminTemporaryPasswordBypassEnabled { get; set; } = false;
}
