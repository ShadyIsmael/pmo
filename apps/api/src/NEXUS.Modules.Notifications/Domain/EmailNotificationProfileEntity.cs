namespace NEXUS.Modules.Notifications.Domain;

/// <summary>
/// Persisted settings for a single Platform, Tenant or Organization SMTP profile.
/// This is intentionally not tenant-filtered: Platform rows have no tenant, and
/// every repository query must spell out the requested scope and owner.
/// SMTP credentials are stored as protected ciphertext by the persistence store.
/// </summary>
public sealed class EmailNotificationProfileEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Scope { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public Guid? OwnerId { get; set; }
    public Guid? OrganizationId { get; set; }

    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string SecurityMode { get; set; } = "StartTlsRequired";
    public string AuthenticationMode { get; set; } = "UsernamePassword";
    public string? UserName { get; set; }
    public string? ProtectedPassword { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string? ReplyToAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
