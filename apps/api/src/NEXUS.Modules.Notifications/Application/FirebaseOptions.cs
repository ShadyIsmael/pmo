namespace NEXUS.Modules.Notifications.Application;

public sealed class FirebaseOptions
{
    public string ProjectId { get; set; } = string.Empty;
    public string CredentialsPath { get; set; } = string.Empty;
    public string CredentialsJson { get; set; } = string.Empty;
    public bool AllowApplicationDefaultCredentials { get; set; }
}
